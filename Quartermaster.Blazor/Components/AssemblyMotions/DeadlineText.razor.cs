using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

/// <summary>Shows a deadline in Berlin time; a midnight deadline also names the last valid second of the day before.</summary>
public partial class DeadlineText {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public DateTimeOffset? Value { get; set; }

    [Parameter]
    public string Placeholder { get; set; } = "-";

    private string? LastSecond;

    protected override void OnParametersSet() {
        LastSecond = Value == null ? null : AssemblyMotionDisplay.LastSecondBefore(Value.Value);
    }
}
