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
/// The campaigns shown outside the management pages: public ones to everyone, members-only ones to every signed-in user (with or
/// without member data, whatever their chapter); never archived ones or ones more than 30 days past their deadline.
/// </summary>
public class MemberSignatureCampaigns {
    public static readonly TimeSpan ShownAfterDeadline = TimeSpan.FromDays(30);

    private readonly DbContext _db;
    private readonly ChapterRepository _chapters;

    public MemberSignatureCampaigns(DbContext db, ChapterRepository chapters) {
        _db = db;
        _chapters = chapters;
    }

    public List<MemberSignatureCampaignDTO> Visible(DateTime nowUtc, bool signedIn) {
        return VisibleCampaigns(nowUtc, signedIn)
            .Select(ToDto)
            .ToList();
    }

    public SignatureCampaign? Find(Guid campaignId, DateTime nowUtc, bool signedIn) {
        return VisibleCampaigns(nowUtc, signedIn).FirstOrDefault(c => c.Id == campaignId);
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
            MemberNotice = campaign.MemberNotice,
            PostalAddress = campaign.PostalAddress,
            ContactEmail = campaign.ContactEmail
        };
    }

    private List<SignatureCampaign> VisibleCampaigns(DateTime nowUtc, bool signedIn) {
        var shownFrom = nowUtc - ShownAfterDeadline;
        var visibilities = new List<SignatureCampaignVisibility> { SignatureCampaignVisibility.Public };
        if (signedIn)
            visibilities.Add(SignatureCampaignVisibility.MembersOnly);

        return _db.SignatureCampaigns
            .Where(c => visibilities.Contains(c.Visibility) && c.ArchivedAt == null && c.Deadline >= shownFrom)
            .OrderBy(c => c.Deadline)
            .ToList();
    }
}
