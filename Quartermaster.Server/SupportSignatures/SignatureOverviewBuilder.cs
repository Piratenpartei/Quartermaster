using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

public class SignatureOverviewBuilder {
    private readonly DbContext _db;
    private readonly AdministrativeDivisionRepository _divisions;

    public SignatureOverviewBuilder(DbContext db, AdministrativeDivisionRepository divisions) {
        _db = db;
        _divisions = divisions;
    }

    public SignatureOverviewDTO Build(SignatureCampaign campaign, DateTime nowUtc) {
        var tallies = SignatureTally.Load(_db, campaign.Id);
        return new SignatureOverviewDTO {
            Totals = SignatureTally.Totals(tallies, campaign.Target, campaign.MarginPercent),
            Rejections = Rejections(campaign.Id),
            Regions = Regions(tallies),
            DaysLeft = (int)Math.Ceiling((campaign.Deadline - nowUtc).TotalDays)
        };
    }

    private List<RejectionCountDTO> Rejections(Guid campaignId) {
        return _db.SignatureReturnLines
            .InnerJoin(_db.SignatureReturns, (l, r) => l.ReturnId == r.Id, (l, r) => new { Line = l, r.ShipmentId })
            .InnerJoin(_db.SignatureShipments, (x, s) => x.ShipmentId == s.Id, (x, s) => new { x.Line, s.CampaignId })
            .Where(x => x.CampaignId == campaignId && x.Line.Rejected > 0)
            .Select(x => x.Line)
            .ToList()
            .GroupBy(l => l.RejectionReason)
            .Select(g => new RejectionCountDTO { Reason = g.Key, Count = g.Sum(l => l.Rejected) })
            .OrderByDescending(r => r.Count)
            .ToList();
    }

    private List<SignatureRegionRowDTO> Regions(Dictionary<Guid, MunicipalityTally> tallies) {
        var municipalities = _divisions.GetMany(tallies.Keys);
        var parents = _divisions.GetMany(municipalities.Where(m => m.ParentId != null).Select(m => m.ParentId!.Value))
            .ToDictionary(p => p.Id);

        return municipalities
            .GroupBy(m => m.ParentId ?? m.Id)
            .Select(g => RegionRow(g.Key, g.ToList(), parents, tallies))
            .OrderBy(r => r.Name)
            .ToList();
    }

    private static SignatureRegionRowDTO RegionRow(Guid regionId, List<AdministrativeDivision> municipalities,
        Dictionary<Guid, AdministrativeDivision> parents, Dictionary<Guid, MunicipalityTally> tallies) {
        var children = municipalities
            .Select(m => Row(m.Id, m.Name, tallies[m.Id]))
            .OrderBy(r => r.Name)
            .ToList();
        return new SignatureRegionRowDTO {
            DivisionId = regionId,
            Name = parents.TryGetValue(regionId, out var parent) ? parent.Name : municipalities[0].Name,
            Received = children.Sum(c => c.Received),
            Certified = children.Sum(c => c.Certified),
            Rejected = children.Sum(c => c.Rejected),
            Children = children
        };
    }

    private static SignatureRegionRowDTO Row(Guid id, string name, MunicipalityTally tally) {
        return new SignatureRegionRowDTO {
            DivisionId = id,
            Name = name,
            Received = tally.Received,
            Certified = tally.CertifiedTotal,
            Rejected = tally.Rejected
        };
    }
}
