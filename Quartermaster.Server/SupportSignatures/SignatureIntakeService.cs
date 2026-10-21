using System;
using System.Collections.Generic;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>
/// Records incoming forms. A correction may never take away forms that already went into a shipment (confirmed or prepared),
/// so every change runs under the campaign lock and is checked against the municipality's plannable count.
/// </summary>
public class SignatureIntakeService {
    private readonly SignatureIntakeRepository _intakes;
    private readonly SignatureCampaignRepository _campaigns;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly AuditLogRepository _auditLog;
    private readonly DbContext _db;

    public SignatureIntakeService(SignatureIntakeRepository intakes, SignatureCampaignRepository campaigns, AdministrativeDivisionRepository divisions,
        AuditLogRepository auditLog, DbContext db) {
        _intakes = intakes;
        _campaigns = campaigns;
        _divisions = divisions;
        _auditLog = auditLog;
        _db = db;
    }

    public (SignatureIntake? Intake, SupportSignatureOutcome Outcome) Create(Guid campaignId, SignatureIntakeSaveRequest request, Guid userId) {
        if (!IsMunicipality(request.AdministrativeDivisionId))
            return (null, SupportSignatureOutcome.Fail(400, I18nKey.Error.SupportSignatures.Intake.NotAMunicipality));

        var intake = new SignatureIntake {
            CampaignId = campaignId,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        Apply(intake, request);
        _intakes.Create(intake);
        _auditLog.LogCreated("SignatureIntake", intake.Id);
        return (intake, SupportSignatureOutcome.Ok());
    }

    public SupportSignatureOutcome Update(SignatureIntake intake, SignatureIntakeSaveRequest request) {
        if (!IsMunicipality(request.AdministrativeDivisionId))
            return SupportSignatureOutcome.Fail(400, I18nKey.Error.SupportSignatures.Intake.NotAMunicipality);

        using var tx = _campaigns.BeginTransaction();
        _campaigns.Lock(intake.CampaignId);
        var current = _intakes.Get(intake.Id);
        if (current == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Intake.NotFound);

        var changed = new SignatureIntake { Id = current.Id, CampaignId = current.CampaignId };
        Apply(changed, request);
        if (!KeepsPlannable(current, changed))
            return SupportSignatureOutcome.Fail(409, I18nKey.Error.SupportSignatures.Intake.AlreadySent);

        _intakes.Update(changed);
        _auditLog.Log("SignatureIntake", current.Id, "Updated", "Count", $"{current.AdministrativeDivisionId}:{current.Count}",
            $"{changed.AdministrativeDivisionId}:{changed.Count}");
        tx.Commit();
        return SupportSignatureOutcome.Ok();
    }

    public SupportSignatureOutcome Delete(SignatureIntake intake) {
        using var tx = _campaigns.BeginTransaction();
        _campaigns.Lock(intake.CampaignId);
        var current = _intakes.Get(intake.Id);
        if (current == null)
            return SupportSignatureOutcome.Fail(404, I18nKey.Error.SupportSignatures.Intake.NotFound);
        if (!KeepsPlannable(current, null))
            return SupportSignatureOutcome.Fail(409, I18nKey.Error.SupportSignatures.Intake.AlreadySent);

        _intakes.Delete(current.Id);
        _auditLog.Log("SignatureIntake", current.Id, "Deleted", "Count", $"{current.AdministrativeDivisionId}:{current.Count}", null);
        tx.Commit();
        return SupportSignatureOutcome.Ok();
    }

    private bool IsMunicipality(Guid divisionId) {
        return _divisions.Get(divisionId)?.Depth == AdministrativeDivisionRepository.MunicipalityDepth;
    }

    /// <summary>Whether replacing <paramref name="current"/> by <paramref name="replacement"/> (null: deleting it) leaves no municipality with more forms shipped than received.</summary>
    private bool KeepsPlannable(SignatureIntake current, SignatureIntake? replacement) {
        var tallies = SignatureTally.Load(_db, current.CampaignId);
        var change = new Dictionary<Guid, int> { [current.AdministrativeDivisionId] = -WaitingShare(current) };
        if (replacement != null)
            change[replacement.AdministrativeDivisionId] = change.GetValueOrDefault(replacement.AdministrativeDivisionId) + WaitingShare(replacement);

        foreach (var (municipalityId, delta) in change) {
            var plannable = tallies.TryGetValue(municipalityId, out var tally) ? tally.Plannable : 0;
            if (plannable + delta < 0)
                return false;
        }

        return true;
    }

    private static int WaitingShare(SignatureIntake intake) {
        return intake.AlreadyCertified ? 0 : intake.Count;
    }

    private static void Apply(SignatureIntake intake, SignatureIntakeSaveRequest request) {
        intake.AdministrativeDivisionId = request.AdministrativeDivisionId;
        intake.Count = request.Count;
        intake.AlreadyCertified = request.AlreadyCertified;
        intake.ReceivedOn = request.ReceivedOn.ToDateTime(TimeOnly.MinValue);
        intake.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
    }
}
