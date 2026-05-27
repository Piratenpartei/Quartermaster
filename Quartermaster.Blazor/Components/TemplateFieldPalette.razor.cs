using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Options;

namespace Quartermaster.Blazor.Components;

public partial class TemplateFieldPalette {
    /// <summary>Comma-separated model identifiers (matches <c>OptionDefinition.TemplateModels</c>).</summary>
    [Parameter]
    public string Models { get; set; } = "";

    /// <summary>Fired with the Fluid expression (e.g. <c>{{ motion.Title }}</c>) when the user clicks the + button.</summary>
    [Parameter]
    public EventCallback<string> OnInsertField { get; set; }

    /// <summary>Globals are always available in every template, so the palette always shows them first.</summary>
    [Parameter]
    public bool IncludeGlobals { get; set; } = true;

    private List<TemplateModelSchemaDTO> Schemas = new();

    protected override void OnParametersSet() {
        Schemas = BuildSchemas(Models, IncludeGlobals);
    }

    private static readonly Dictionary<string, (string Prefix, Type Type)> ReflectedModels = new() {
        ["MembershipApplicationDetailDTO"] = ("application", typeof(MembershipApplicationDetailDTO)),
        ["DueSelectionDetailDTO"] = ("selection", typeof(DueSelectionDetailDTO)),
        ["ChapterDTO"] = ("chapter", typeof(ChapterDTO)),
        ["MotionDTO"] = ("motion", typeof(MotionDTO))
    };

    /// <summary>
    /// Notification trigger payloads are anonymous-typed dictionaries built in the server's
    /// model factory, so we can't reflect them out of a class. The shapes below MUST stay
    /// in sync with the model factories in <c>MotionCreateEndpoint</c>,
    /// <c>MembershipApplicationCreateEndpoint</c>, and <c>DueSelectionCreateEndpoint</c>.
    /// </summary>
    private static readonly Dictionary<string, List<TemplateModelSchemaDTO>> NotificationSchemas = new() {
        ["MotionSubmittedPayload"] = new() {
            new() {
                ModelName = "Antrag", VariablePrefix = "motion",
                Fields = new() {
                    Field("Id", "Guid", "motion"),
                    Field("Title", "string", "motion"),
                    Field("AuthorName", "string", "motion"),
                    Field("CreatedAt", "DateTime", "motion")
                }
            },
            ChapterMiniSchema()
        },
        ["ApplicationSubmittedPayload"] = new() {
            new() {
                ModelName = "Mitgliedsantrag", VariablePrefix = "application",
                Fields = new() {
                    Field("Id", "Guid", "application"),
                    Field("FirstName", "string", "application"),
                    Field("LastName", "string", "application"),
                    Field("Email", "string", "application"),
                    Field("SubmittedAt", "DateTime", "application"),
                    Field("HasReducedDueSelection", "bool", "application")
                }
            },
            ChapterMiniSchema()
        },
        ["DueSelectionSubmittedPayload"] = new() {
            new() {
                ModelName = "Beitragseinstufung", VariablePrefix = "selection",
                Fields = new() {
                    Field("Id", "Guid", "selection"),
                    Field("FirstName", "string", "selection"),
                    Field("LastName", "string", "selection"),
                    Field("Email", "string", "selection"),
                    Field("SelectedDue", "decimal", "selection"),
                    Field("ReducedAmount", "decimal?", "selection"),
                    Field("ReducedJustification", "string?", "selection")
                }
            },
            ChapterMiniSchema()
        }
    };

    private static TemplateModelSchemaDTO GlobalsSchema() => new() {
        ModelName = "globals",
        VariablePrefix = "globals",
        Fields = new() {
            Field("base_url", "string", "globals"),
            Field("app_name", "string", "globals"),
            Field("now", "DateTime", "globals")
        }
    };

    private static TemplateModelSchemaDTO ChapterMiniSchema() => new() {
        ModelName = "Gliederung", VariablePrefix = "chapter",
        Fields = new() {
            Field("Id", "Guid", "chapter"),
            Field("Name", "string", "chapter")
        }
    };

    private static TemplateFieldDTO Field(string name, string type, string prefix) => new() {
        Name = name, Type = type, FluidExpression = $"{{{{ {prefix}.{name} }}}}"
    };

    private static List<TemplateModelSchemaDTO> BuildSchemas(string models, bool includeGlobals) {
        var result = new List<TemplateModelSchemaDTO>();
        if (includeGlobals) {
            result.Add(GlobalsSchema());
        }
        var seenPrefixes = new HashSet<string>(result.Select(s => s.VariablePrefix));
        foreach (var modelName in models.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (NotificationSchemas.TryGetValue(modelName, out var schemas)) {
                foreach (var s in schemas) {
                    if (seenPrefixes.Add(s.VariablePrefix)) {
                        result.Add(s);
                    }
                }
                continue;
            }
            if (ReflectedModels.TryGetValue(modelName, out var entry)) {
                if (!seenPrefixes.Add(entry.Prefix)) {
                    continue;
                }
                result.Add(SchemaFromReflection(modelName, entry.Prefix, entry.Type));
            }
        }
        return result;
    }

    private static TemplateModelSchemaDTO SchemaFromReflection(string modelName, string prefix, Type type) {
        var fields = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !IsComplexType(p.PropertyType))
            .Select(p => new TemplateFieldDTO {
                Name = p.Name,
                Type = FriendlyTypeName(p.PropertyType),
                FluidExpression = $"{{{{ {prefix}.{p.Name} }}}}"
            })
            .ToList();
        return new TemplateModelSchemaDTO {
            ModelName = modelName,
            VariablePrefix = prefix,
            Fields = fields
        };
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
        if (underlying != null) {
            return FriendlyTypeName(underlying) + "?";
        }
        if (type == typeof(string)) {
            return "string";
        }
        if (type == typeof(int)) {
            return "int";
        }
        if (type == typeof(decimal)) {
            return "decimal";
        }
        if (type == typeof(bool)) {
            return "bool";
        }
        if (type == typeof(DateTime)) {
            return "DateTime";
        }
        if (type == typeof(Guid)) {
            return "Guid";
        }
        return type.Name;
    }
}
