using System;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class LocalTime {
    [Parameter]
    public DateTimeOffset? Value { get; set; }

    [Parameter]
    public DateOnly? DateValue { get; set; }

    [Parameter]
    public string Format { get; set; } = "dd.MM.yyyy HH:mm";

    [Parameter]
    public string DateFormat { get; set; } = "dd.MM.yyyy";
}
