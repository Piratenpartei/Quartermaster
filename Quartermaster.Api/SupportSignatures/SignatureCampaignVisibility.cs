namespace Quartermaster.Api.SupportSignatures;

/// <summary>Who sees a campaign on the start page and its detail page; managers always see it.</summary>
public enum SignatureCampaignVisibility {
    Public = 0,
    MembersOnly = 1,
    Private = 2
}
