using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.Options;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

public enum SettingsStatus {
    None,
    Configured,
    Partial,
    NotConfigured,
    On,
    Off
}

public static class SettingsStatusResolver {
    public static SettingsStatus Resolve(SettingsAssistant assistant, IReadOnlyDictionary<string, OptionDefinitionDTO> options) {
        if (assistant.ToggleKeys.Count > 0)
            return assistant.ToggleKeys.Any(k => IsOn(k, options)) ? SettingsStatus.On : SettingsStatus.Off;
        if (assistant.RequiredKeys.Count == 0)
            return SettingsStatus.None;

        var set = assistant.RequiredKeys.Count(k => options.TryGetValue(k, out var option) && !string.IsNullOrWhiteSpace(option.GlobalValue));
        if (set == assistant.RequiredKeys.Count)
            return SettingsStatus.Configured;

        return set == 0 ? SettingsStatus.NotConfigured : SettingsStatus.Partial;
    }

    private static bool IsOn(string key, IReadOnlyDictionary<string, OptionDefinitionDTO> options) {
        if (!options.TryGetValue(key, out var option))
            return false;
        if (IsTrue(option.GlobalValue))
            return true;

        return option.Overrides.Any(o => IsTrue(o.Value));
    }

    private static bool IsTrue(string value) {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
