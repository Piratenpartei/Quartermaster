using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

public partial class SettingsSectionCard {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public SettingsSection Section { get; set; } = default!;

    [Parameter]
    public EventCallback OnSaved { get; set; }

    private OptionGroupEditor? _editor;
    private SettingsCheckResultDTO? CheckResult;
    private IReadOnlyList<string> CheckItems = [];
    private bool Busy;

    private async Task Save() {
        if (_editor == null)
            return;

        Busy = true;
        try {
            if (await _editor.SaveAsync())
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } finally {
            Busy = false;
        }
    }

    /// <summary>Checks run against the stored values, so pending edits are saved first.</summary>
    private async Task SaveAndCheck() {
        if (_editor == null || Section.Check == null)
            return;

        Busy = true;
        CheckResult = null;
        try {
            if (!await _editor.SaveAsync())
                return;

            var response = await Http.PostAsJsonAsync("/api/settings/check", new SettingsCheckRequest { Kind = Section.Check.Value });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            CheckResult = await response.Content.ReadFromJsonAsync<SettingsCheckResultDTO>();
            if (CheckResult?.Success == true)
                CheckItems = CheckResult.Items;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    // Fields that take the items (suggestions, fill values) already show them.
    private bool ShowItems() {
        if (CheckResult == null || CheckResult.Items.Count == 0)
            return false;

        return !Section.Fields.Any(f => f.FillFromCheck || f.Kind == SettingsFieldKind.Suggest);
    }
}
