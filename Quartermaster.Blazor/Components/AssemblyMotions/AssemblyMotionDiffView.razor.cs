using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionDiffView {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required List<TextDiffLine> Lines { get; set; }

    private static string LineClass(TextDiffKind kind) => kind switch {
        TextDiffKind.Added => "bg-success-subtle text-success-emphasis",
        TextDiffKind.Removed => "bg-danger-subtle text-danger-emphasis",
        _ => ""
    };

    private static string Prefix(TextDiffKind kind) => kind switch {
        TextDiffKind.Added => "+",
        TextDiffKind.Removed => "-",
        _ => " "
    };
}
