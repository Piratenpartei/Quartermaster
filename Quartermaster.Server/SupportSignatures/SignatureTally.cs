using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>The numbers of a campaign, per municipality and in total, computed from its intakes, shipments and returns.</summary>
public static class SignatureTally {
    public static Dictionary<Guid, MunicipalityTally> PerMunicipality(IEnumerable<SignatureIntake> intakes, IEnumerable<SignatureShipmentLine> sentLines,
        IEnumerable<SignatureShipmentLine> preparedLines, IEnumerable<SignatureReturnLine> returnLines) {
        var received = Sum(intakes, i => i.AdministrativeDivisionId, i => i.Count);
        var alreadyCertified = Sum(intakes.Where(i => i.AlreadyCertified), i => i.AdministrativeDivisionId, i => i.Count);
        var sent = Sum(sentLines, l => l.AdministrativeDivisionId, l => l.Count);
        var prepared = Sum(preparedLines, l => l.AdministrativeDivisionId, l => l.Count);
        var certified = Sum(returnLines, l => l.AdministrativeDivisionId, l => l.Certified);
        var rejected = Sum(returnLines, l => l.AdministrativeDivisionId, l => l.Rejected);

        return received.Keys
            .Concat(sent.Keys)
            .Concat(prepared.Keys)
            .Distinct()
            .ToDictionary(id => id, id => new MunicipalityTally(
                received.GetValueOrDefault(id),
                alreadyCertified.GetValueOrDefault(id),
                sent.GetValueOrDefault(id),
                prepared.GetValueOrDefault(id),
                certified.GetValueOrDefault(id),
                rejected.GetValueOrDefault(id)));
    }

    public static Dictionary<Guid, MunicipalityTally> Load(DbContext db, Guid campaignId) {
        var intakes = db.SignatureIntakes
            .Where(i => i.CampaignId == campaignId)
            .ToList();
        var lines = db.SignatureShipmentLines
            .InnerJoin(db.SignatureShipments, (l, s) => l.ShipmentId == s.Id, (l, s) => new { Line = l, s.CampaignId, s.SentOn })
            .Where(x => x.CampaignId == campaignId)
            .ToList();
        var returnLines = db.SignatureReturnLines
            .InnerJoin(db.SignatureReturns, (l, r) => l.ReturnId == r.Id, (l, r) => new { Line = l, r.ShipmentId })
            .InnerJoin(db.SignatureShipments, (x, s) => x.ShipmentId == s.Id, (x, s) => new { x.Line, s.CampaignId })
            .Where(x => x.CampaignId == campaignId)
            .Select(x => x.Line)
            .ToList();

        return PerMunicipality(
            intakes,
            lines.Where(x => x.SentOn != null).Select(x => x.Line),
            lines.Where(x => x.SentOn == null).Select(x => x.Line),
            returnLines);
    }

    public static SignatureTotalsDTO Totals(IReadOnlyDictionary<Guid, MunicipalityTally> tallies, int target, int marginPercent) {
        var values = tallies.Values.ToList();
        return new SignatureTotalsDTO {
            Received = values.Sum(t => t.Received),
            AlreadyCertified = values.Sum(t => t.AlreadyCertified),
            Waiting = values.Sum(t => t.Waiting),
            AtOffices = values.Sum(t => t.Open),
            Certified = values.Sum(t => t.CertifiedTotal),
            Rejected = values.Sum(t => t.Rejected),
            Target = target,
            Goal = Goal(target, marginPercent)
        };
    }

    /// <summary>The target plus the safety margin, rounded up so the margin is never undercut.</summary>
    public static int Goal(int target, int marginPercent) {
        return (target * (100 + marginPercent) + 99) / 100;
    }

    private static Dictionary<Guid, int> Sum<T>(IEnumerable<T> items, Func<T, Guid> key, Func<T, int> value) {
        return items
            .GroupBy(key)
            .ToDictionary(g => g.Key, g => g.Sum(value));
    }
}
