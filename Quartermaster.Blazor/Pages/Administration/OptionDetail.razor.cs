using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Options;
using Quartermaster.Api.Rendering;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class OptionDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public string Identifier { get; set; } = "";

    private OptionDefinitionDTO? Option;
    private List<ChapterDTO>? Chapters;
    private List<TemplateModelSchemaDTO>? Schemas;
    private bool Loading = true;
    private string NewOverrideChapterId { get; set; } = "";
    private string? PreviewHtml;
    private bool ShowPreview;
    private OptionOverrideDTO? EditingOverride;
    private string EditingOverrideValue { get; set; } = "";
    private CancellationTokenSource? _previewDebounce;

    private static readonly Dictionary<string, (string Prefix, Type Type)> ModelMap = new() {
        ["MembershipApplicationDetailDTO"] = ("application", typeof(MembershipApplicationDetailDTO)),
        ["DueSelectionDetailDTO"] = ("selection", typeof(DueSelectionDetailDTO)),
        ["ChapterDTO"] = ("chapter", typeof(ChapterDTO))
    };

    protected override async Task OnInitializedAsync() {
        try {
            var decodedIdentifier = System.Net.WebUtility.UrlDecode(Identifier);
            Chapters = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");

            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");
            Option = options?.FirstOrDefault(o => o.Identifier == decodedIdentifier);

            if (Option?.DataType == 2 && !string.IsNullOrEmpty(Option.TemplateModels))
                Schemas = BuildSchemas(Option.TemplateModels);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
    }

    private static List<TemplateModelSchemaDTO> BuildSchemas(string templateModels) {
        var models = templateModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<TemplateModelSchemaDTO>();

        foreach (var modelName in models) {
            if (!ModelMap.TryGetValue(modelName, out var entry))
                continue;

            var fields = entry.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !IsComplexType(p.PropertyType))
                .Select(p => new TemplateFieldDTO {
                    Name = p.Name,
                    Type = FriendlyTypeName(p.PropertyType),
                    FluidExpression = $"{{{{ {entry.Prefix}.{p.Name} }}}}"
                })
                .ToList();

            result.Add(new TemplateModelSchemaDTO {
                ModelName = modelName,
                VariablePrefix = entry.Prefix,
                Fields = fields
            });
        }

        return result;
    }

    private static bool IsComplexType(Type type) {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return !underlying.IsPrimitive
            && underlying != typeof(string)
            && underlying != typeof(decimal)
            && underlying != typeof(DateTime)
            && underlying != typeof(Guid);
    }

    private static string FriendlyTypeName(Type type) {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return FriendlyTypeName(underlying) + "?";

        if (type == typeof(string))
            return "string";
        if (type == typeof(int))
            return "int";
        if (type == typeof(decimal))
            return "decimal";
        if (type == typeof(bool))
            return "bool";
        if (type == typeof(DateTime))
            return "DateTime";
        if (type == typeof(Guid))
            return "Guid";
        return type.Name;
    }

    private async Task OnTemplateInput(ChangeEventArgs e) {
        if (Option == null)
            return;

        Option.GlobalValue = e.Value?.ToString() ?? "";

        if (!ShowPreview)
            return;

        _previewDebounce?.Cancel();
        _previewDebounce = new CancellationTokenSource();
        var token = _previewDebounce.Token;

        try {
            await Task.Delay(500, token);
            await UpdatePreview();
        } catch (TaskCanceledException) { }
    }

    private async Task SaveGlobal() {
        if (Option == null)
            return;

        try {
            await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                Identifier = Option.Identifier,
                ChapterId = null,
                Value = Option.GlobalValue
            });
            ToastService.Toast("Gespeichert.", "success");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task SaveOverride(Guid chapterId, string value) {
        if (Option == null)
            return;

        try {
            await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                Identifier = Option.Identifier,
                ChapterId = chapterId,
                Value = value
            });
            ToastService.Toast("Gespeichert.", "success");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task AddOverride() {
        if (Option == null || !Guid.TryParse(NewOverrideChapterId, out var chapterId))
            return;

        try {
            await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                Identifier = Option.Identifier,
                ChapterId = chapterId,
                Value = Option.GlobalValue
            });

            NewOverrideChapterId = "";
            await ReloadOption();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private void LoadOverrideForEditing(OptionOverrideDTO ov) {
        EditingOverride = ov;
        EditingOverrideValue = ov.Value;
        StateHasChanged();
    }

    private async Task SaveEditingOverride() {
        if (EditingOverride == null || Option == null)
            return;

        await SaveOverride(EditingOverride.ChapterId, EditingOverrideValue);
        EditingOverride = null;
        await ReloadOption();
    }

    private void CancelEditingOverride() {
        EditingOverride = null;
        StateHasChanged();
    }

    private async Task TogglePreview() {
        if (Option == null)
            return;

        ShowPreview = !ShowPreview;

        if (ShowPreview) {
            await UpdatePreview();
        } else {
            StateHasChanged();
        }
    }

    private async Task UpdatePreview() {
        if (Option == null)
            return;

        var mockData = TemplateMockDataProvider.GetMockData(Option.TemplateModels);
        var (html, error) = await TemplateRenderer.RenderAsync(Option.GlobalValue, mockData);

        if (error != null)
            PreviewHtml = $"<p class=\"text-danger\">{error}</p>";
        else
            PreviewHtml = html ?? "";

        StateHasChanged();
    }

    private async Task InsertField(string fluidExpression) {
        if (Option == null)
            return;

        Option.GlobalValue += fluidExpression;

        if (ShowPreview) {
            await UpdatePreview();
        } else {
            StateHasChanged();
        }
    }

    private async Task ReloadOption() {
        try {
            var decodedIdentifier = System.Net.WebUtility.UrlDecode(Identifier);
            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");
            Option = options?.FirstOrDefault(o => o.Identifier == decodedIdentifier);
            StateHasChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private static string DataTypeLabel(int dt) => dt switch {
        0 => "Text",
        1 => "Zahl",
        2 => "Template",
        _ => "?"
    };

    private static string DataTypeBadge(int dt) => dt switch {
        0 => "border-info text-info-emphasis",
        1 => "border-primary text-primary-emphasis",
        2 => "border-warning text-warning-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };
}
