using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

public enum SettingsGroup {
    Basics,
    Login,
    Communication,
    Integrations,
    Assembly
}

public record SettingsAssistant(string Slug, SettingsGroup Group, string Icon, string TitleKey, string DescriptionKey) {
    public IReadOnlyList<SettingsSection> Sections { get; init; } = [];

    /// <summary>Options the assistant sets through its own flow rather than as plain fields.</summary>
    public IReadOnlyList<string> ManagedKeys { get; init; } = [];

    /// <summary>All of them set means configured, some of them partially configured.</summary>
    public IReadOnlyList<string> RequiredKeys { get; init; } = [];

    /// <summary>When set, the card shows On as soon as one of them is true, globally or for a chapter.</summary>
    public IReadOnlyList<string> ToggleKeys { get; init; } = [];

    public IReadOnlyList<SettingsLink> Links { get; init; } = [];

    public string Route { get; } = $"/Administration/Setup/{Slug}";

    public IEnumerable<string> AllKeys() {
        return Sections
            .SelectMany(s => s.Fields)
            .Select(f => f.Key)
            .Concat(ManagedKeys);
    }
}
