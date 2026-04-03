using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Forms;

public partial class FormTextarea {
    [CascadingParameter]
    public DirtyForm? Form { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public int Rows { get; set; } = 5;

    [Parameter]
    public string CssClass { get; set; } = "";

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    private async Task OnInput(ChangeEventArgs e) {
        Value = e.Value?.ToString() ?? "";
        await ValueChanged.InvokeAsync(Value);
        Form?.MarkDirty();
    }
}
