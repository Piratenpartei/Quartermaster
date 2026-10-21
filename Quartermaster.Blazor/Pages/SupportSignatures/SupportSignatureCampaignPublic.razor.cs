using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Services;
using Quartermaster.Rendering;

namespace Quartermaster.Blazor.Pages.SupportSignatures;

/// <summary>The members' view of a campaign: progress, notice and form download, but no regions or offices.</summary>
public partial class SupportSignatureCampaignPublic {
    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MemberSignatureCampaignDTO? Campaign;
    private MarkupString? NoticeHtml;
    private bool Loading = true;

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        try {
            await AuthService.WaitForInitializationAsync();
            if (!AuthService.IsAuthenticated) {
                Navigation.NavigateTo("/Login", replace: true);
                return;
            }

            Campaign = await Signatures.MemberCampaignAsync(Id);
            NoticeHtml = string.IsNullOrWhiteSpace(Campaign?.MemberNotice)
                ? null
                : new MarkupString(MarkdownService.ToHtml(Campaign.MemberNotice, SanitizationProfile.Strict));
        } finally {
            Loading = false;
        }
    }

    private async Task DownloadForm() {
        if (Campaign != null)
            await Signatures.DownloadMemberFormAsync(Campaign.Id, Campaign.Name);
    }
}
