using System.Collections.Generic;
using System.Text.Json;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

/// <summary>
/// Boundary translator between the typed event-template/checklist DTOs on the wire
/// and the JSON text stored in <c>EventTemplate.Variables</c>,
/// <c>EventTemplate.ChecklistItemTemplates</c>, and <c>EventChecklistItem.Configuration</c>.
/// </summary>
internal static class EventConfigSerializer {
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, Options);

    public static List<EventTemplateVariableDTO> ParseVariables(string? json) {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        return JsonSerializer.Deserialize<List<EventTemplateVariableDTO>>(json, Options) ?? [];
    }

    public static List<EventChecklistItemTemplateDTO> ParseTemplates(string? json) {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        return JsonSerializer.Deserialize<List<EventChecklistItemTemplateDTO>>(json, Options) ?? [];
    }

    public static EventChecklistItemConfigDTO? ParseConfig(string? json) {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        return JsonSerializer.Deserialize<EventChecklistItemConfigDTO>(json, Options);
    }

    /// <summary>Per-field <c>{{name}}</c> substitution across every string property that can carry user content.</summary>
    public static EventChecklistItemConfigDTO? ApplyVariables(EventChecklistItemConfigDTO? config, IReadOnlyDictionary<string, string> values) {
        if (config == null)
            return null;
        return new EventChecklistItemConfigDTO {
            UseDescription = config.UseDescription,
            TargetType = Replace(config.TargetType, values),
            TemplateId = config.TemplateId,
            ManualAddresses = Replace(config.ManualAddresses, values),
            TargetId = config.TargetId,
            ChapterId = config.ChapterId,
            MotionTitle = Replace(config.MotionTitle, values),
            MotionText = Replace(config.MotionText, values)
        };
    }

    private static string? Replace(string? text, IReadOnlyDictionary<string, string> values) {
        if (string.IsNullOrEmpty(text))
            return text;
        foreach (var (name, value) in values)
            text = text!.Replace($"{{{{{name}}}}}", value);
        return text;
    }
}
