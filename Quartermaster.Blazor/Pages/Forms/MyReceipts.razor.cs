using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Receipts;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Forms;

/// <summary>The logged-in user's receipt cache; receipts stay until the user deletes them so recurring routes can reuse them.</summary>
public partial class MyReceipts {
    private const int MaxFilesPerUpload = 20;
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ReceiptService Receipts { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }

    private UserReceiptListDTO? List;
    private ConfirmDialog DeleteConfirm = default!;
    private bool Uploading;
    private Guid? EditingId;
    private string EditingLabel = "";

    protected override async Task OnInitializedAsync() {
        await AuthService.WaitForInitializationAsync();
        if (!AuthService.IsAuthenticated) {
            Navigation.NavigateTo("/Login", replace: true);
            return;
        }
        List = await Receipts.ListAsync(reload: true);
    }

    private bool LimitReached() {
        return List?.MaxCount != null && List.Items.Count >= List.MaxCount.Value;
    }

    private string UsageText() {
        if (List == null)
            return "";
        if (List.MaxCount == null)
            return I18n[I18nParams.With(I18nKey.Ui.Receipts.UsageUnlimited, ("count", List.Items.Count.ToString()), ("mb", List.MaxFileMegabytes.ToString()))];

        return I18n[I18nParams.With(I18nKey.Ui.Receipts.Usage, ("count", List.Items.Count.ToString()), ("max", List.MaxCount.Value.ToString()), ("mb", List.MaxFileMegabytes.ToString()))];
    }

    private async Task Upload(InputFileChangeEventArgs args) {
        Uploading = true;
        foreach (var file in args.GetMultipleFiles(MaxFilesPerUpload)) {
            if (await Receipts.UploadAsync(file) == null)
                break;
        }
        List = await Receipts.ListAsync();
        Uploading = false;
    }

    private void StartEdit(UserReceiptDTO receipt) {
        EditingId = receipt.Id;
        EditingLabel = receipt.Label;
    }

    private void CancelEdit() {
        EditingId = null;
    }

    private async Task SaveLabel() {
        if (EditingId == null || string.IsNullOrWhiteSpace(EditingLabel))
            return;
        if (!await Receipts.RenameAsync(EditingId.Value, EditingLabel.Trim()))
            return;

        EditingId = null;
        List = await Receipts.ListAsync();
    }

    private async Task Delete(UserReceiptDTO receipt) {
        if (!await DeleteConfirm.ShowAsync())
            return;
        if (await Receipts.DeleteAsync(receipt.Id))
            List = await Receipts.ListAsync();
    }

    private static string SizeText(long bytes) {
        if (bytes < 1024 * 1024)
            return $"{Math.Max(1, bytes / 1024)} KB";

        return (bytes / 1024d / 1024d).ToString("0.0", German) + " MB";
    }
}
