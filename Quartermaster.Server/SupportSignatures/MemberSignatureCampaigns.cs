using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>
/// The campaigns every signed-in user sees, with or without member data and whatever their chapter: visible, not archived, and at most
/// 30 days past their deadline.
/// </summary>
public class MemberSignatureCampaigns {
    public static readonly TimeSpan ShownAfterDeadline = TimeSpan.FromDays(30);

    private readonly DbContext _db;
    private readonly ChapterRepository _chapters;

    public MemberSignatureCampaigns(DbContext db, ChapterRepository chapters) {
        _db = db;
        _chapters = chapters;
    }

    public List<MemberSignatureCampaignDTO> Visible(DateTime nowUtc) {
        return VisibleCampaigns(nowUtc)
            .Select(ToDto)
            .ToList();
    }

    public SignatureCampaign? Find(Guid campaignId, DateTime nowUtc) {
        return VisibleCampaigns(nowUtc).FirstOrDefault(c => c.Id == campaignId);
    }

    public MemberSignatureCampaignDTO ToDto(SignatureCampaign campaign) {
        var totals = SignatureTally.Totals(SignatureTally.Load(_db, campaign.Id), campaign.Target, campaign.MarginPercent);
        return new MemberSignatureCampaignDTO {
            Id = campaign.Id,
            Name = campaign.Name,
            ChapterName = _chapters.Get(campaign.ChapterId)?.Name ?? "",
            Target = campaign.Target,
            Goal = totals.Goal,
            CertifiedTotal = totals.Certified,
            CollectedTotal = totals.Received - totals.Rejected,
            Deadline = campaign.Deadline.ToDtoUtc(),
            HasForm = campaign.HasForm,
            MemberNotice = campaign.MemberNotice
        };
    }

    private List<SignatureCampaign> VisibleCampaigns(DateTime nowUtc) {
        var shownFrom = nowUtc - ShownAfterDeadline;
        return _db.SignatureCampaigns
            .Where(c => c.VisibleToMembers && c.ArchivedAt == null && c.Deadline >= shownFrom)
            .OrderBy(c => c.Deadline)
            .ToList();
    }
}
