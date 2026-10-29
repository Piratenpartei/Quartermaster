using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class NextTakeOverNotice {
    [Parameter, EditorRequired]
    public DateTimeOffset At { get; set; }

    [Parameter, EditorRequired]
    public string ChapterName { get; set; } = "";

    [Inject]
    public required I18nService I18n { get; set; }
}
