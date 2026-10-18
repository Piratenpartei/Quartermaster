using System.Collections.Generic;

namespace Quartermaster.Api.Settings;

public class SettingsCheckRequest {
    public SettingsCheckKind Kind { get; set; }
}

public class SettingsCheckResultDTO {
    public bool Success { get; set; }

    /// <summary>An i18n key, possibly carrying parameters via <c>I18nParams.With</c>.</summary>
    public string MessageKey { get; set; } = "";

    /// <summary>The raw error text of a failed check, untranslated.</summary>
    public string? Detail { get; set; }

    /// <summary>What the check found: model ids, channel names or the bot username.</summary>
    public List<string> Items { get; set; } = [];
}
