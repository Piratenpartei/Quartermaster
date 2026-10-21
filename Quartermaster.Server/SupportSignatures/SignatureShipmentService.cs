using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>
/// Prepares, confirms and discards shipments and records their returns. Every change first locks the campaign row and then
/// reads the numbers, so two people preparing at the same time never put the same waiting forms into two shipments.
/// </summary>
public class SignatureShipmentService {
    private readonly SignatureShipmentRepository _shipments;
    private readonly SignatureCampaignRepository _campaigns;
    private readonly CertificationOfficeRepository _offices;
    private readonly AuditLogRepository _auditLog;
    private readonly DbContext _db;

    public SignatureShipmentService(SignatureShipmentRepository shipments, SignatureCampaignRepository campaigns, CertificationOfficeRepository offices,
        AuditLogRepository auditLog, DbContext db) {
        _shipments = shipments;
        _campaigns = campaigns;
        _offices = offices;
        _auditLog = auditLog;
        _db = db;
    }

    public ShipmentPlan Plan(Guid campaignId) {
        var tallies = SignatureTally.Load(_db, campaignId);
        return ShipmentPlanner.Plan(tallies, _offices.GetOfficeIdsByMunicipality(tallies.Keys));
    }

    public (List<Guid> Ids, SupportSignatureOutcome Outcome) Prepare(Guid campaignId, IReadOnlyCollection<Guid> officeIds, Guid userId) {
        using var tx = _campaigns.BeginTransaction();
        _campaigns.Lock(campaignId);

        var ids = new List<Guid>();
        foreach (var planned in Plan(campaignId).Offices.Where(o => officeIds.Contains(o.OfficeId))) {
            ids.Add(Create(campaignId, planned.OfficeId, planned.Lines, userId));
        }

        tx.Commit();
        return (ids, SupportSignatureOutcome.Ok());
    }

    public (Guid? Id, SupportSignatureOutcome Outcome) CreateSingle(Guid campaignId, Guid officeId, IReadOnlyList<PlannedLine> lines, Guid userId) {
        if (_offices.Get(officeId) == null)
            return (null, SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Shipment.OfficeNotFound));

        var merged = lines
            .GroupBy(l => l.MunicipalityId)
            .Select(g => new PlannedLine(g.Key, g.Sum(l => l.Count)))
            .Where(l => l.Count > 0)
            .ToList();
        if (merged.Count == 0)
            return (null, SupportSignatureOutcome.Fail(400, I18nKey.Error.SupportSignatures.Shipment.Empty));

        var officeByMunicipality = _offices.GetOfficeIdsByMunicipality(merged.Select(l => l.MunicipalityId));
        if (merged.Any(l => officeByMunicipality.GetValueOrDefault(l.MunicipalityId) != officeId))
            return (null, SupportSignatureOutcome.Fail(400, I18nKey.Error.SupportSignatures.Shipment.WrongOffice));

        using var tx = _campaigns.BeginTransaction();
        _campaigns.Lock(campaignId);
        var tallies = SignatureTally.Load(_db, campaignId);
        if (merged.Any(l => l.Count > (tallies.TryGetValue(l.MunicipalityId, out var tally) ? tally.Plannable : 0)))
            return (null, SupportSignatureOutcome.Fail(409, I18nKey.Error.SupportSignatures.Shipment.ExceedsWaiting));

        var id = Create(campaignId, officeId, merged, userId);
        tx.Commit();
        return (id, SupportSignatureOutcome.Ok());
    }

    public SupportSignatureOutcome Confirm(Guid shipmentId, DateOnly sentOn) {
        var shipment = _shipments.Get(shipmentId);
        if (shipment == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Shipment.NotFound);

        using var tx = _campaigns.BeginTransaction();
        _campaigns.Lock(shipment.CampaignId);
        var current = _shipments.Get(shipmentId);
        if (current == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Shipment.NotFound);
        if (current.SentOn != null)
            return SupportSignatureOutcome.Fail(409, I18nKey.Error.SupportSignatures.Shipment.AlreadySent);

        _shipments.SetSentOn(shipmentId, sentOn.ToDateTime(TimeOnly.MinValue));
        _auditLog.Log("SignatureShipment", shipmentId, "Confirmed", "SentOn", null, sentOn.ToString("yyyy-MM-dd"));
        tx.Commit();
        return SupportSignatureOutcome.Ok();
    }

    public SupportSignatureOutcome Discard(Guid shipmentId) {
        var shipment = _shipments.Get(shipmentId);
        if (shipment == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Shipment.NotFound);

        using var tx = _campaigns.BeginTransaction();
        _campaigns.Lock(shipment.CampaignId);
        var current = _shipments.Get(shipmentId);
        if (current == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Shipment.NotFound);
        if (current.SentOn != null)
            return SupportSignatureOutcome.Fail(409, I18nKey.Error.SupportSignatures.Shipment.AlreadySent);

        _shipments.Delete(shipmentId);
        _auditLog.Log("SignatureShipment", shipmentId, "Discarded");
        tx.Commit();
        return SupportSignatureOutcome.Ok();
    }

    public SupportSignatureOutcome RecordReturn(Guid shipmentId, DateOnly returnedOn, IReadOnlyList<ReturnLineInput> lines, Guid userId) {
        var shipment = _shipments.Get(shipmentId);
        if (shipment == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Shipment.NotFound);
        if (lines.Count == 0)
            return SupportSignatureOutcome.Fail(400, I18nKey.Error.SupportSignatures.Return.Empty);

        using var tx = _campaigns.BeginTransaction();
        _campaigns.Lock(shipment.CampaignId);
        var current = _shipments.Get(shipmentId);
        if (current == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Shipment.NotFound);
        if (current.SentOn == null)
            return SupportSignatureOutcome.Fail(409, I18nKey.Error.SupportSignatures.Return.NotSent);

        var problem = ReturnProblem(shipmentId, lines, null);
        if (problem != null)
            return problem;

        var signatureReturn = new SignatureReturn {
            ShipmentId = shipmentId,
            ReturnedOn = returnedOn.ToDateTime(TimeOnly.MinValue),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        _shipments.CreateReturn(signatureReturn, ToReturnLines(lines));
        _auditLog.Log("SignatureReturn", signatureReturn.Id, "Created", "Counts", null, CountsText(lines));
        tx.Commit();
        return SupportSignatureOutcome.Ok();
    }

    public SupportSignatureOutcome UpdateReturn(Guid returnId, DateOnly returnedOn, IReadOnlyList<ReturnLineInput> lines) {
        var signatureReturn = _shipments.GetReturn(returnId);
        var shipment = signatureReturn == null ? null : _shipments.Get(signatureReturn.ShipmentId);
        if (signatureReturn == null || shipment == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Return.NotFound);
        if (lines.Count == 0)
            return SupportSignatureOutcome.Fail(400, I18nKey.Error.SupportSignatures.Return.Empty);

        using var tx = _campaigns.BeginTransaction();
        _campaigns.Lock(shipment.CampaignId);
        if (_shipments.GetReturn(returnId) == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Return.NotFound);

        var problem = ReturnProblem(shipment.Id, lines, returnId);
        if (problem != null)
            return problem;

        var before = _shipments.GetReturnLines([returnId]);
        _shipments.ReplaceReturnLines(returnId, returnedOn.ToDateTime(TimeOnly.MinValue), ToReturnLines(lines));
        _auditLog.Log("SignatureReturn", returnId, "Updated", "Counts",
            $"{before.Sum(l => l.Certified)} bestätigt, {before.Sum(l => l.Rejected)} abgelehnt", CountsText(lines));
        tx.Commit();
        return SupportSignatureOutcome.Ok();
    }

    public SupportSignatureOutcome DeleteReturn(Guid returnId) {
        var signatureReturn = _shipments.GetReturn(returnId);
        var shipment = signatureReturn == null ? null : _shipments.Get(signatureReturn.ShipmentId);
        if (signatureReturn == null || shipment == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Return.NotFound);

        using var tx = _campaigns.BeginTransaction();
        _campaigns.Lock(shipment.CampaignId);
        _shipments.DeleteReturn(returnId);
        _auditLog.Log("SignatureReturn", returnId, "Deleted");
        tx.Commit();
        return SupportSignatureOutcome.Ok();
    }

    public List<SignatureShipmentDTO> List(Guid campaignId, DateOnly today) {
        var campaign = _campaigns.Get(campaignId);
        if (campaign == null)
            return [];

        var shipments = _shipments.List(campaignId);
        var lines = _shipments.GetLines(shipments.Select(s => s.Id));
        var returns = _shipments.GetReturns(shipments.Select(s => s.Id));
        var returnLines = _shipments.GetReturnLines(returns.Select(r => r.Id));
        var offices = _offices.List().ToDictionary(o => o.Id, o => o.Name);
        var municipalityIds = lines
            .Select(l => l.AdministrativeDivisionId)
            .Distinct()
            .ToList();
        var municipalityNames = _db.AdministrativeDivisions
            .Where(d => municipalityIds.Contains(d.Id))
            .ToDictionary(d => d.Id, d => d.Name);

        return shipments
            .Select(s => SignatureShipmentView.Build(s, offices.GetValueOrDefault(s.OfficeId, ""),
                lines.Where(l => l.ShipmentId == s.Id).ToList(),
                returns.Where(r => r.ShipmentId == s.Id).ToList(),
                returnLines, municipalityNames, campaign.OverdueDays, today))
            .ToList();
    }

    /// <summary>The number of overdue shipments per campaign, in three queries for all campaigns together.</summary>
    public Dictionary<Guid, int> OverdueCounts(IReadOnlyList<SignatureCampaign> campaigns, DateOnly today) {
        var campaignIds = campaigns.Select(c => c.Id).ToList();
        var sent = _db.SignatureShipments
            .Where(s => campaignIds.Contains(s.CampaignId) && s.SentOn != null)
            .ToList();
        var shipmentIds = sent.Select(s => s.Id).ToList();
        var sentCounts = _db.SignatureShipmentLines
            .Where(l => shipmentIds.Contains(l.ShipmentId))
            .GroupBy(l => l.ShipmentId)
            .Select(g => new { ShipmentId = g.Key, Count = g.Sum(l => l.Count) })
            .ToDictionary(x => x.ShipmentId, x => x.Count);
        var returnedCounts = _db.SignatureReturnLines
            .InnerJoin(_db.SignatureReturns, (l, r) => l.ReturnId == r.Id, (l, r) => new { r.ShipmentId, Count = l.Certified + l.Rejected })
            .Where(x => shipmentIds.Contains(x.ShipmentId))
            .GroupBy(x => x.ShipmentId)
            .Select(g => new { ShipmentId = g.Key, Count = g.Sum(x => x.Count) })
            .ToDictionary(x => x.ShipmentId, x => x.Count);

        var sentByCampaign = sent.ToLookup(s => s.CampaignId);
        return campaigns.ToDictionary(c => c.Id, c => sentByCampaign[c.Id]
            .Count(s => IsOverdue(s, c.OverdueDays, sentCounts.GetValueOrDefault(s.Id), returnedCounts.GetValueOrDefault(s.Id), today)));
    }

    private static bool IsOverdue(SignatureShipment shipment, int overdueDays, int sentCount, int returnedCount, DateOnly today) {
        if (shipment.SentOn == null || returnedCount >= sentCount)
            return false;

        return DateOnly.FromDateTime(shipment.SentOn.Value).AddDays(overdueDays) < today;
    }

    private Guid Create(Guid campaignId, Guid officeId, IEnumerable<PlannedLine> lines, Guid userId) {
        var shipment = new SignatureShipment {
            CampaignId = campaignId,
            OfficeId = officeId,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        _shipments.Create(shipment, lines.Select(l => new SignatureShipmentLine { AdministrativeDivisionId = l.MunicipalityId, Count = l.Count }).ToList());
        _auditLog.Log("SignatureShipment", shipment.Id, "Prepared");
        return shipment.Id;
    }

    private static IEnumerable<SignatureReturnLine> ToReturnLines(IReadOnlyList<ReturnLineInput> lines) {
        return lines.Select(l => new SignatureReturnLine {
            AdministrativeDivisionId = l.MunicipalityId,
            Certified = l.Certified,
            Rejected = l.Rejected,
            RejectionReason = l.Rejected > 0 ? l.RejectionReason : null
        });
    }

    private static string CountsText(IReadOnlyList<ReturnLineInput> lines) {
        return $"{lines.Sum(l => l.Certified)} bestätigt, {lines.Sum(l => l.Rejected)} abgelehnt";
    }

    /// <summary>Checks the lines against what the shipment sent, counting earlier returns except <paramref name="replacedReturnId"/>, which the lines replace.</summary>
    private SupportSignatureOutcome? ReturnProblem(Guid shipmentId, IReadOnlyList<ReturnLineInput> lines, Guid? replacedReturnId) {
        var sent = _shipments.GetLines([shipmentId]).ToDictionary(l => l.AdministrativeDivisionId, l => l.Count);
        if (lines.Any(l => !sent.ContainsKey(l.MunicipalityId)))
            return SupportSignatureOutcome.Fail(400, I18nKey.Error.SupportSignatures.Return.NotInShipment);

        var earlierReturnIds = _shipments.GetReturns([shipmentId])
            .Select(r => r.Id)
            .Where(id => id != replacedReturnId);
        var returned = _shipments.GetReturnLines(earlierReturnIds)
            .GroupBy(l => l.AdministrativeDivisionId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Certified + l.Rejected));
        var exceeds = lines
            .GroupBy(l => l.MunicipalityId)
            .Any(g => returned.GetValueOrDefault(g.Key) + g.Sum(l => l.Certified + l.Rejected) > sent[g.Key]);
        return exceeds ? SupportSignatureOutcome.Fail(409, I18nKey.Error.SupportSignatures.Return.ExceedsOpen) : null;
    }
}
