using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components;

public partial class OptionGroupEditor {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    /// <summary>Option identifiers to edit, in display order.</summary>
    [Parameter]
    public required string[] Keys { get; set; }

    /// <summary>When true each field takes a full row; otherwise two per row on wide screens.</summary>
    [Parameter]
    public bool FullWidthFields { get; set; }

    /// <summary>Invoked after a successful save — e.g. to refresh client config the saved options feed into.</summary>
    [Parameter]
    public EventCallback OnSaved { get; set; }

    private List<OptionDefinitionDTO>? Fields;
    private readonly Dictionary<string, string> _values = new();
    private bool Saving;

    protected override async Task OnInitializedAsync() {
        try {
            var all = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options") ?? new();
            var byId = all.ToDictionary(o => o.Identifier);
            Fields = Keys
                .Where(byId.ContainsKey)
                .Select(k => byId[k])
                .ToList();
            foreach (var field in Fields) {
                // Secrets start blank so we never echo or re-save the stored value unless changed.
                _values[field.Identifier] = field.IsSecret ? "" : field.GlobalValue;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private string ColumnClass(OptionDefinitionDTO field) {
        if (FullWidthFields || IsBool(field)) {
            return "col-12";
        }
        return "col-12 col-md-6";
    }

    private static bool IsBool(OptionDefinitionDTO field) => field.Identifier.EndsWith("use_ssl", StringComparison.Ordinal);

    private bool BoolValue(string identifier) => _values.TryGetValue(identifier, out var v)
        && v.Equals("true", StringComparison.OrdinalIgnoreCase);

    private void SetBool(string identifier, bool value) {
        _values[identifier] = value ? "true" : "false";
    }

    private void OnInput(string identifier, ChangeEventArgs e) {
        _values[identifier] = e.Value?.ToString() ?? "";
    }

    private bool HasStoredSecret(OptionDefinitionDTO field) => !string.IsNullOrEmpty(field.GlobalValue);

    private async Task Save() {
        if (Fields == null) {
            return;
        }
        Saving = true;
        StateHasChanged();
        try {
            foreach (var field in Fields) {
                var value = _values[field.Identifier];
                // Don't overwrite a stored secret with an empty input (means "leave unchanged").
                if (field.IsSecret && string.IsNullOrEmpty(value)) {
                    continue;
                }
                var resp = await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                    Identifier = field.Identifier,
                    ChapterId = null,
                    Value = value
                });
                if (!resp.IsSuccessStatusCode) {
                    await ToastService.ErrorAsync(resp);
                    return;
                }
            }
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            await OnSaved.InvokeAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Saving = false;
            StateHasChanged();
        }
    }
}
