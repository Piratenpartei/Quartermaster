using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Components.AssemblyMotions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.SupportSignatures;

public partial class CampaignSettingsTab {
    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required ToastService Toasts { get; set; }

    [Parameter, EditorRequired]
    public SignatureCampaignDTO Campaign { get; set; } = default!;

    [Parameter]
    public EventCallback<SignatureCampaignDTO> Changed { get; set; }

    private SignatureCampaignSaveRequest Request = new();
    private string Deadline = "";
    private ConfirmDialog Confirm = default!;
    private bool Busy;
    private Guid LoadedCampaignId;

    protected override void OnParametersSet() {
        if (Campaign.Id == LoadedCampaignId)
            return;

        LoadedCampaignId = Campaign.Id;
        Request = new SignatureCampaignSaveRequest {
            ChapterId = Campaign.ChapterId,
            Name = Campaign.Name,
            Target = Campaign.Target,
            MarginPercent = Campaign.MarginPercent,
            Deadline = Campaign.Deadline,
            OverdueDays = Campaign.OverdueDays,
            Visibility = Campaign.Visibility,
            MemberNotice = Campaign.MemberNotice,
            PostalAddress = Campaign.PostalAddress,
            ContactEmail = Campaign.ContactEmail
        };
        Deadline = AssemblyMotionDisplay.ToInput(Campaign.Deadline);
    }

    private async Task Save() {
        Request.Deadline = AssemblyMotionDisplay.FromInput(Deadline) ?? Campaign.Deadline;
        await Apply(Signatures.UpdateCampaignAsync(Campaign.Id, Request));
    }

    private async Task Upload(InputFileChangeEventArgs args) {
        await Apply(Signatures.UploadFormAsync(Campaign.Id, args.File));
    }

    private async Task DownloadForm() {
        await Signatures.DownloadCampaignFormAsync(Campaign);
    }

    private async Task RemoveForm() {
        if (!await Confirm.ShowAsync())
            return;

        Busy = true;
        var removed = await Signatures.DeleteFormAsync(Campaign.Id);
        Busy = false;
        if (removed)
            await Reload();
    }

    private async Task Archive() {
        if (!await Confirm.ShowAsync(I18n[I18nKey.Ui.SupportSignatures.ArchiveConfirm]))
            return;

        await Apply(Signatures.ArchiveCampaignAsync(Campaign.Id));
    }

    private async Task Apply(Task<SignatureCampaignDTO?> call) {
        Busy = true;
        var updated = await call;
        Busy = false;
        if (updated == null)
            return;

        Toasts.ToastKey(I18nKey.Ui.Toast.Saved);
        await Changed.InvokeAsync(updated);
    }

    private async Task Reload() {
        var reloaded = await Signatures.CampaignAsync(Campaign.Id);
        if (reloaded != null)
            await Changed.InvokeAsync(reloaded);
    }
}
