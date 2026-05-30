using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Members;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Options;
using Quartermaster.Api.Templates;

namespace Quartermaster.Blazor.Components;

public partial class TemplateFieldPalette {
    [Inject]
    public required IJSRuntime JS { get; set; }

    [Parameter]
    public string Models { get; set; } = "";

    [Parameter]
    public EventCallback<string> OnInsertField { get; set; }

    [Parameter]
    public bool IncludeGlobals { get; set; } = true;

    private List<TemplateModelSchemaDTO> Schemas = new();
    private ElementReference _card;
    private bool _fitInstalled;

    protected override void OnParametersSet() {
        Schemas = BuildSchemas(Models, IncludeGlobals);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (_fitInstalled || Schemas.Count == 0)
            return;
        _fitInstalled = true;
        await JS.InvokeVoidAsync("StickyFitToViewport", _card, 16);
    }

    private static readonly Dictionary<string, (string Prefix, string LabelKey, Type Type)> ReflectedModels = new() {
        ["TemplateGlobalsDTO"] = ("globals", I18nKey.Ui.TemplateFieldPalette.ModelGlobals, typeof(TemplateGlobalsDTO)),
        ["TemplateConfirmationDTO"] = ("confirm", I18nKey.Ui.TemplateFieldPalette.ModelConfirmation, typeof(TemplateConfirmationDTO)),
        ["ChapterDTO"] = ("chapter", I18nKey.Ui.TemplateFieldPalette.ModelChapter, typeof(ChapterDTO)),
        ["MembershipApplicationDetailDTO"] = ("application", I18nKey.Ui.TemplateFieldPalette.ModelApplication, typeof(MembershipApplicationDetailDTO)),
        ["DueSelectionDetailDTO"] = ("selection", I18nKey.Ui.TemplateFieldPalette.ModelSelection, typeof(DueSelectionDetailDTO)),
        ["MotionDetailDTO"] = ("motion", I18nKey.Ui.TemplateFieldPalette.ModelMotion, typeof(MotionDetailDTO)),
        ["MemberDetailDTO"] = ("member", I18nKey.Ui.TemplateFieldPalette.ModelMember, typeof(MemberDetailDTO)),
        ["EventDetailDTO"] = ("event", I18nKey.Ui.TemplateFieldPalette.ModelEvent, typeof(EventDetailDTO))
    };

    private static List<TemplateModelSchemaDTO> BuildSchemas(string models, bool includeGlobals) {
        var result = new List<TemplateModelSchemaDTO>();
        var seenPrefixes = new HashSet<string>();
        if (includeGlobals) {
            var globals = SchemaFor("TemplateGlobalsDTO");
            if (globals != null) {
                result.Add(globals);
                seenPrefixes.Add(globals.VariablePrefix);
            }
        }
        foreach (var modelName in models.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            var schema = SchemaFor(modelName);
            if (schema == null)
                continue;
            if (!seenPrefixes.Add(schema.VariablePrefix))
                continue;
            result.Add(schema);
        }
        return result;
    }

    private static TemplateModelSchemaDTO? SchemaFor(string modelName) {
        if (!ReflectedModels.TryGetValue(modelName, out var entry))
            return null;
        return SchemaFromReflection(entry.LabelKey, entry.Prefix, entry.Type);
    }

    private static TemplateModelSchemaDTO SchemaFromReflection(string labelKey, string prefix, Type type) {
        var fields = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !IsComplexType(p.PropertyType))
            .Select(p => new TemplateFieldDTO {
                Name = p.Name,
                Type = FriendlyTypeName(p.PropertyType),
                FluidExpression = $"{{{{ {prefix}.{p.Name} }}}}"
            })
            .ToList();
        return new TemplateModelSchemaDTO {
            ModelName = labelKey,
            VariablePrefix = prefix,
            Fields = fields
        };
    }

    private static bool IsComplexType(Type type) {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return !underlying.IsPrimitive
            && !underlying.IsEnum
            && underlying != typeof(string)
            && underlying != typeof(decimal)
            && underlying != typeof(DateTime)
            && underlying != typeof(DateTimeOffset)
            && underlying != typeof(DateOnly)
            && underlying != typeof(TimeOnly)
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
        if (type == typeof(DateTimeOffset))
            return "DateTimeOffset";
        if (type == typeof(DateOnly))
            return "DateOnly";
        if (type == typeof(Guid))
            return "Guid";
        return type.Name;
    }
}
