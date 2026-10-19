using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Receipts;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Receipts;

/// <summary>Attaches receipts from the user's cache to a list of ids, uploading new ones on the way.</summary>
public partial class ReceiptPicker : IDisposable {
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ReceiptService Receipts { get; set; }

    [Parameter, EditorRequired]
    public List<Guid> SelectedIds { get; set; } = default!;

    /// <summary>Off for anonymous users, who attach their receipts to the printed form by hand.</summary>
    [Parameter]
    public bool Enabled { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    private List<UserReceiptDTO> Owned = [];
    private bool MenuOpen;
    private bool Uploading;

    protected override async Task OnInitializedAsync() {
        if (!Enabled)
            return;

        Receipts.Changed += OnReceiptsChanged;
        Owned = (await Receipts.ListAsync()).Items;
    }

    public void Dispose() {
        Receipts.Changed -= OnReceiptsChanged;
    }

    private void OnReceiptsChanged() {
        _ = InvokeAsync(async () => {
            Owned = (await Receipts.ListAsync()).Items;
            StateHasChanged();
        });
    }

    private IEnumerable<UserReceiptDTO> Selected() {
        return SelectedIds
            .Select(id => Owned.FirstOrDefault(r => r.Id == id))
            .Where(r => r != null)
            .Select(r => r!);
    }

    /// <summary>Receipts deleted from the cache after they were attached; they are left out of the PDF.</summary>
    private int MissingCount() {
        return SelectedIds.Count(id => Owned.All(r => r.Id != id));
    }

    private IEnumerable<UserReceiptDTO> Available() {
        return Owned.Where(r => !SelectedIds.Contains(r.Id));
    }

    private void ToggleMenu() {
        MenuOpen = !MenuOpen;
    }

    private async Task Attach(Guid id) {
        SelectedIds.Add(id);
        MenuOpen = false;
        await OnChanged.InvokeAsync();
    }

    private async Task Detach(Guid id) {
        SelectedIds.Remove(id);
        await OnChanged.InvokeAsync();
    }

    private async Task Upload(InputFileChangeEventArgs args) {
        MenuOpen = false;
        Uploading = true;
        var created = await Receipts.UploadAsync(args.File);
        Uploading = false;
        if (created == null)
            return;

        Owned = (await Receipts.ListAsync()).Items;
        await Attach(created.Id);
    }
}
