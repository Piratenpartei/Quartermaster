using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.Forms;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Forms;

public partial class MyForms {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required IJSRuntime JS { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }

    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private List<LocalDraftEntry> LocalDrafts = new();
    private List<ExpenseReimbursementListItemDTO> ServerItems = new();
    private bool LocalLoading = true;
    private bool ServerLoading;
    private ConfirmDialog DeleteConfirm = default!;

    protected override async Task OnInitializedAsync() {
        if (AuthService.IsAuthenticated) {
            ServerLoading = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender) {
            return;
        }
        await LoadLocalDrafts();
        if (AuthService.IsAuthenticated) {
            await LoadServerItems();
        }
    }

    private async Task LoadLocalDrafts() {
        LocalLoading = true;
        StateHasChanged();
        var drafts = new List<LocalDraftEntry>();
        foreach (var entry in FormCatalog.Entries) {
            var prefix = LocalDraftKeys.PrefixFor(entry.FormKey);
            string[] keys;
            try {
                keys = await JS.InvokeAsync<string[]>("qmStorage.listKeys", prefix) ?? Array.Empty<string>();
            } catch (Exception) {
                continue;
            }
            foreach (var key in keys) {
                try {
                    var raw = await JS.InvokeAsync<string?>("qmStorage.load", key);
                    if (string.IsNullOrEmpty(raw)) {
                        continue;
                    }
                    var metadata = JsonSerializer.Deserialize<LocalDraftMetadata>(raw);
                    if (metadata != null) {
                        drafts.Add(new LocalDraftEntry {
                            Entry = entry,
                            StorageKey = key,
                            Metadata = metadata
                        });
                    }
                } catch (Exception) {
                }
            }
        }
        LocalDrafts = drafts
            .OrderByDescending(d => d.Metadata.UpdatedAt)
            .ToList();
        LocalLoading = false;
        StateHasChanged();
    }

    private async Task LoadServerItems() {
        ServerLoading = true;
        StateHasChanged();
        try {
            ServerItems = await Http.GetFromJsonAsync<List<ExpenseReimbursementListItemDTO>>("/api/forms/expense-reimbursement")
                ?? new();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        ServerLoading = false;
        StateHasChanged();
    }

    private async Task DeleteLocal(LocalDraftEntry draft) {
        var ok = await DeleteConfirm.ShowAsync();
        if (!ok) {
            return;
        }
        try {
            await JS.InvokeVoidAsync("qmStorage.clear", draft.StorageKey);
            await LoadLocalDrafts();
        } catch (Exception ex) {
            ToastService.Error(ex.Message);
        }
    }

    private async Task DownloadServer(ExpenseReimbursementListItemDTO item) {
        try {
            var resp = await Http.GetAsync($"/api/forms/expense-reimbursement/{item.Id}/pdf");
            if (!resp.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(resp);
                return;
            }
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            await JS.InvokeVoidAsync("qmDownload.blob", bytes, "auslagenerstattung.pdf", "application/pdf");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CopyServer(ExpenseReimbursementListItemDTO item) {
        try {
            var resp = await Http.PostAsJsonAsync($"/api/forms/expense-reimbursement/{item.Id}/copy", new { });
            if (!resp.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(resp);
                return;
            }
            var created = await resp.Content.ReadFromJsonAsync<ExpenseReimbursementResponseDTO>();
            if (created != null) {
                Navigation.NavigateTo($"/Formulare/auslagenerstattung/{created.Id}");
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task DeleteServer(ExpenseReimbursementListItemDTO item) {
        var ok = await DeleteConfirm.ShowAsync();
        if (!ok) {
            return;
        }
        try {
            var resp = await Http.DeleteAsync($"/api/forms/expense-reimbursement/{item.Id}");
            if (!resp.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(resp);
                return;
            }
            await LoadServerItems();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private class LocalDraftEntry {
        public required FormCatalogEntry Entry { get; set; }
        public required string StorageKey { get; set; }
        public required LocalDraftMetadata Metadata { get; set; }
    }
}
