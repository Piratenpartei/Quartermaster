using System;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class LocalTime {
    /// <summary>Timestamp to render in the browser's local time. Null renders nothing.</summary>
    [Parameter]
    public DateTimeOffset? Value { get; set; }

    /// <summary>Calendar date to render. Null renders nothing.</summary>
    [Parameter]
    public DateOnly? DateValue { get; set; }

    [Parameter]
    public string Format { get; set; } = "dd.MM.yyyy HH:mm";

    [Parameter]
    public string DateFormat { get; set; } = "dd.MM.yyyy";
}
