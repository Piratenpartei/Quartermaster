using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Pages.Administration.Settings;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components;

public partial class OptionGroupEditor {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    /// <summary>The options to edit, in display order.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<SettingsField> Fields { get; set; } = [];

    /// <summary>When true each field takes a full row; otherwise two per row on wide screens.</summary>
    [Parameter]
    public bool FullWidthFields { get; set; }

    /// <summary>When false the caller saves through <see cref="SaveAsync"/>, e.g. right before a connection check.</summary>
    [Parameter]
    public bool ShowSaveButton { get; set; } = true;

    /// <summary>Invoked after a successful save, e.g. to refresh client config the saved options feed into.</summary>
    [Parameter]
    public EventCallback OnSaved { get; set; }

    /// <summary>Items of the last successful connection check: suggestions for suggest fields and the value for fill fields.</summary>
    [Parameter]
    public IReadOnlyList<string> CheckItems { get; set; } = [];

    private List<Row>? Rows;
    private readonly Dictionary<string, string> _values = new();
    private readonly Dictionary<string, string> _original = new();
    private readonly HashSet<string> _filled = new();
    private IReadOnlyList<string> _appliedCheckItems = [];
    private bool Saving;

    private record Row(SettingsField Field, OptionDefinitionDTO Definition);

    protected override Task OnInitializedAsync() => ReloadAsync();

    protected override void OnParametersSet() {
        if (ReferenceEquals(CheckItems, _appliedCheckItems) || Rows == null)
            return;

        _appliedCheckItems = CheckItems;
        if (CheckItems.Count == 0)
            return;

        foreach (var row in Rows.Where(r => r.Field.FillFromCheck)) {
            if (_values[row.Field.Key] == CheckItems[0])
                continue;

            _values[row.Field.Key] = CheckItems[0];
            _filled.Add(row.Field.Key);
        }
    }

    /// <summary>Re-fetches the definitions and current values, e.g. after a sibling component wrote option values directly.</summary>
    public async Task ReloadAsync() {
        try {
            var all = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options") ?? new();
            var byId = all.ToDictionary(o => o.Identifier);
            Rows = Fields
                .Where(f => byId.ContainsKey(f.Key))
                .Select(f => new Row(f, byId[f.Key]))
                .ToList();
            _values.Clear();
            _original.Clear();
            _filled.Clear();
            foreach (var row in Rows) {
                var display = DisplayValue(row);
                _values[row.Field.Key] = display;
                _original[row.Field.Key] = display;
            }
            StateHasChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    /// <summary>Writes every changed field; false when a value is invalid or the server refused it.</summary>
    public async Task<bool> SaveAsync() {
        if (Rows == null)
            return false;

        var changes = new List<(string Key, string Value)>();
        foreach (var row in Rows) {
            var value = _values[row.Field.Key];
            if (value == _original[row.Field.Key])
                continue;
            // A stored secret is never sent to the client, so an empty input means "leave unchanged".
            if (row.Definition.IsSecret && value.Length == 0)
                continue;

            var stored = StoredValue(row, value);
            if (stored == null) {
                ToastService.ErrorKey(I18nParams.With(I18nKey.Ui.Settings.DurationInvalid, ("field", Label(row))));
                return false;
            }
            changes.Add((row.Field.Key, stored));
        }

        Saving = true;
        StateHasChanged();
        try {
            foreach (var (key, value) in changes) {
                var resp = await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                    Identifier = key,
                    ChapterId = null,
                    Value = value
                });
                if (!resp.IsSuccessStatusCode) {
                    await ToastService.ErrorAsync(resp);
                    return false;
                }
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            return false;
        } finally {
            Saving = false;
            StateHasChanged();
        }

        await ReloadAsync();
        await OnSaved.InvokeAsync();
        return true;
    }

    private async Task SaveClicked() {
        if (await SaveAsync())
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
    }

    private string ColumnClass(Row row) {
        if (FullWidthFields || row.Field.FullWidth || row.Field.Kind == SettingsFieldKind.Multiline)
            return "col-12";

        return "col-12 col-md-6";
    }

    private static string Label(Row row) {
        var name = row.Definition.FriendlyName;
        var separator = name.IndexOf(": ", StringComparison.Ordinal);
        return separator > 0 ? name[(separator + 2)..] : name;
    }

    private static string InputId(Row row) {
        return "option-" + row.Field.Key.Replace('.', '-');
    }

    private string DisplayValue(Row row) {
        if (row.Definition.IsSecret)
            return "";
        if (row.Field.Kind != SettingsFieldKind.Duration)
            return row.Definition.GlobalValue;

        return int.TryParse(row.Definition.GlobalValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? $"{seconds / 60}:{seconds % 60:00}"
            : row.Definition.GlobalValue;
    }

    /// <summary>The value as stored; null when a duration can't be read as m:ss or seconds.</summary>
    private static string? StoredValue(Row row, string value) {
        if (row.Field.Kind != SettingsFieldKind.Duration)
            return value;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return "";

        var parts = trimmed.Split(':');
        if (parts.Length == 1)
            return int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var plain) ? plain.ToString(CultureInfo.InvariantCulture) : null;
        if (parts.Length != 2)
            return null;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
            return null;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var secs) || secs > 59)
            return null;

        return (minutes * 60 + secs).ToString(CultureInfo.InvariantCulture);
    }

    private bool BoolValue(string key) => _values[key].Equals("true", StringComparison.OrdinalIgnoreCase);

    private void SetBool(string key, bool value) {
        _values[key] = value ? "true" : "false";
    }

    private void OnInput(string key, ChangeEventArgs e) {
        _values[key] = e.Value?.ToString() ?? "";
        _filled.Remove(key);
    }

    private void UseCurrentAddress(string key) {
        _values[key] = Navigation.BaseUri.TrimEnd('/');
    }

    private static bool HasStoredSecret(Row row) => !string.IsNullOrEmpty(row.Definition.GlobalValue);
}
