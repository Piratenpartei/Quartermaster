using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>Campaign DTOs with chapter name and current numbers.</summary>
public class SignatureCampaignDtoBuilder {
    private readonly ChapterRepository _chapters;
    private readonly DbContext _db;
    private readonly SignatureShipmentService _shipments;

    public SignatureCampaignDtoBuilder(ChapterRepository chapters, DbContext db, SignatureShipmentService shipments) {
        _chapters = chapters;
        _db = db;
        _shipments = shipments;
    }

    public SignatureCampaignDTO Build(SignatureCampaign campaign) {
        return BuildMany([campaign])[0];
    }

    public List<SignatureCampaignDTO> BuildMany(List<SignatureCampaign> campaigns) {
        var chapterNames = campaigns
            .Select(c => c.ChapterId)
            .Distinct()
            .ToDictionary(id => id, id => _chapters.Get(id)?.Name ?? "");

        var overdue = _shipments.OverdueCounts(campaigns, DateOnly.FromDateTime(DateTime.UtcNow));

        return campaigns
            .Select(c => c.ToDto(chapterNames[c.ChapterId], SignatureTally.Totals(SignatureTally.Load(_db, c.Id), c.Target, c.MarginPercent), overdue[c.Id]))
            .ToList();
    }
}
