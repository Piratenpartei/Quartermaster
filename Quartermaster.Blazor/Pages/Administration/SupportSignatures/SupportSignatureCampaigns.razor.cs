using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Components.AssemblyMotions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.SupportSignatures;

public partial class SupportSignatureCampaigns {
    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private List<SignatureCampaignDTO>? Campaigns;
    private bool ShowArchived;
    private bool Busy;
    private SignatureCampaignSaveRequest? Creating;
    private string CreateChapterId = "";
    private string CreateDeadline = "";

    protected override async Task OnInitializedAsync() {
        await LoadAsync();
    }

    private async Task LoadAsync() {
        Campaigns = null;
        Campaigns = await Signatures.CampaignsAsync(ShowArchived) ?? [];
    }

    private async Task DownloadForm(SignatureCampaignDTO campaign) {
        await Signatures.DownloadCampaignFormAsync(campaign);
    }

    private async Task ToggleArchived(ChangeEventArgs args) {
        ShowArchived = args.Value is true;
        await LoadAsync();
    }

    private void StartCreate() {
        Creating = new SignatureCampaignSaveRequest { Target = 2000 };
        CreateChapterId = "";
        CreateDeadline = "";
    }

    private void CancelCreate() {
        Creating = null;
    }

    private async Task Create() {
        if (Creating == null)
            return;

        Creating.ChapterId = Guid.TryParse(CreateChapterId, out var chapterId) ? chapterId : Guid.Empty;
        Creating.Deadline = AssemblyMotionDisplay.FromInput(CreateDeadline) ?? default;
        Busy = true;
        var created = await Signatures.CreateCampaignAsync(Creating);
        Busy = false;
        if (created != null)
            Navigation.NavigateTo($"/Administration/SupportSignatures/{created.Id}");
    }
}
