using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.TravelExpenseRules;

/// <summary>Versions of the travel expense rules per chapter and of the global default the lookup falls back to.</summary>
public partial class TravelExpenseRuleAdmin {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }

    private TravelExpenseRuleScopesDTO? Scopes;
    private List<TravelExpenseRulePresetDTO> Presets = [];
    private List<TravelExpenseRulesDTO> Versions = [];
    private TravelExpenseRulesDTO? Editing;
    private ConfirmDialog DeleteConfirm = default!;
    private bool GlobalSelected;
    private Guid? ChapterId;
    private bool NewMenuOpen;
    private bool Busy;

    protected override async Task OnInitializedAsync() {
        await AuthService.WaitForInitializationAsync();
        if (!AuthService.IsAuthenticated) {
            Navigation.NavigateTo("/Login", replace: true);
            return;
        }

        try {
            Scopes = await Http.GetFromJsonAsync<TravelExpenseRuleScopesDTO>("/api/travel-expense-rules/scopes");
            if (Scopes == null || (!Scopes.CanEditGlobal && Scopes.ChapterIds?.Count == 0))
                return;

            Presets = await Http.GetFromJsonAsync<List<TravelExpenseRulePresetDTO>>("/api/travel-expense-rules/presets") ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            return;
        }

        if (Scopes.CanEditGlobal) {
            GlobalSelected = true;
            await LoadVersions();
        } else if (Scopes.ChapterIds?.Count == 1) {
            ChapterId = Scopes.ChapterIds[0];
            await LoadVersions();
        }
    }

    private async Task SelectGlobal() {
        GlobalSelected = true;
        await LoadVersions();
    }

    private void SelectChapterScope() {
        GlobalSelected = false;
        Versions = [];
        Editing = null;
        ChapterId = null;
    }

    private async Task OnChapterPicked(string raw) {
        ChapterId = Guid.TryParse(raw, out var id) ? id : null;
        Versions = [];
        Editing = null;
        if (ChapterId != null)
            await LoadVersions();
    }

    private Guid? ScopeChapterId() {
        return GlobalSelected ? null : ChapterId;
    }

    private async Task LoadVersions(Guid? selectId = null) {
        var query = ScopeChapterId() == null ? "" : $"?chapterId={ScopeChapterId()}";
        try {
            Versions = await Http.GetFromJsonAsync<List<TravelExpenseRulesDTO>>($"/api/travel-expense-rules{query}") ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Versions = [];
        }

        var selected = Versions.FirstOrDefault(v => v.Id == selectId) ?? Versions.FirstOrDefault();
        Editing = selected == null ? null : Clone(selected);
    }

    private void Select(TravelExpenseRulesDTO version) {
        Editing = Clone(version);
    }

    private void ToggleNewMenu() {
        NewMenuOpen = !NewMenuOpen;
    }

    private void NewFromNewest() {
        NewMenuOpen = false;
        var newest = Versions.FirstOrDefault();
        if (newest != null)
            StartNew(Clone(newest));
    }

    private void NewFromPreset(TravelExpenseRulePresetDTO preset) {
        NewMenuOpen = false;
        StartNew(Clone(preset.Rules));
    }

    // A new version starts today so it does not silently change trips already made under the previous one.
    private void StartNew(TravelExpenseRulesDTO rules) {
        rules.Id = Guid.Empty;
        rules.ChapterId = ScopeChapterId();
        rules.ValidFrom = DateOnly.FromDateTime(DateTime.Today);
        Editing = rules;
    }

    private async Task DiscardNew() {
        await LoadVersions();
    }

    private async Task Save() {
        if (Editing == null)
            return;

        Busy = true;
        try {
            var response = Editing.Id == Guid.Empty
                ? await Http.PostAsJsonAsync("/api/travel-expense-rules", Editing)
                : await Http.PutAsJsonAsync($"/api/travel-expense-rules/{Editing.Id}", Editing);
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            var saved = await response.Content.ReadFromJsonAsync<TravelExpenseRulesDTO>();
            ToastService.ToastKey(I18nKey.Ui.TravelExpenseRules.Saved);
            await LoadVersions(saved?.Id);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task Delete() {
        if (Editing == null || !await DeleteConfirm.ShowAsync())
            return;

        Busy = true;
        try {
            var response = await Http.DeleteAsync($"/api/travel-expense-rules/{Editing.Id}");
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }
            await LoadVersions();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private static TravelExpenseRulesDTO Clone(TravelExpenseRulesDTO rules) {
        return JsonSerializer.Deserialize<TravelExpenseRulesDTO>(JsonSerializer.Serialize(rules))!;
    }
}
