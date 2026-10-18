using System.Collections.Generic;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

public enum SettingsFieldKind {
    Text,
    Number,
    Bool,
    /// <summary>Stored as seconds, edited as m:ss.</summary>
    Duration,
    Multiline,
    Select,
    /// <summary>Free text with suggestions from the section's connection check.</summary>
    Suggest,
    /// <summary>Text with a button that fills in the address the page is served from.</summary>
    BaseUrl
}

/// <summary>How an assistant edits one option. The label and description come from the option definition.</summary>
public record SettingsField(string Key, SettingsFieldKind Kind = SettingsFieldKind.Text) {
    /// <summary>An i18n key shown as unit after number fields.</summary>
    public string? UnitKey { get; init; }

    public string? Step { get; init; }

    public IReadOnlyList<SettingsChoice> Choices { get; init; } = [];

    public bool FullWidth { get; init; }

    /// <summary>Takes the first item of a successful connection check as new value.</summary>
    public bool FillFromCheck { get; init; }
}

public record SettingsChoice(string Value, string LabelKey);
