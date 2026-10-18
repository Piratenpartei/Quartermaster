using System.Collections.Generic;
using Quartermaster.Api.Settings;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

public record SettingsSection(IReadOnlyList<SettingsField> Fields) {
    public string? TitleKey { get; init; }

    public string? DescriptionKey { get; init; }

    public SettingsCheckKind? Check { get; init; }
}
