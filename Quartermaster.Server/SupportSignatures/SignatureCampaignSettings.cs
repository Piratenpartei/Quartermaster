using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

public static class SignatureCampaignSettings {
    /// <summary>Copies the editable settings of a validated request onto the campaign; the chapter is fixed at creation.</summary>
    public static void Apply(SignatureCampaign campaign, SignatureCampaignSaveRequest request) {
        campaign.Name = request.Name.Trim();
        campaign.Target = request.Target;
        campaign.MarginPercent = request.MarginPercent;
        campaign.Deadline = request.Deadline.UtcDateTime;
        campaign.OverdueDays = request.OverdueDays;
        campaign.VisibleToMembers = request.VisibleToMembers;
        campaign.MemberNotice = string.IsNullOrWhiteSpace(request.MemberNotice) ? null : request.MemberNotice.Trim();
    }
}
