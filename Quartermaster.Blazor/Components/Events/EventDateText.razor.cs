using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.Events;

/// <summary>An event's date with its start time, if one is set.</summary>
public partial class EventDateText {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public DateOnly? Date { get; set; }

    [Parameter]
    public TimeOnly? StartTime { get; set; }
}
