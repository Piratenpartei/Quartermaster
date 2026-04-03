using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Forms;

public partial class FormSelect {
    [CascadingParameter]
    public DirtyForm? Form { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public string CssClass { get; set; } = "";

    [Parameter]
    public bool Disabled { get; set; }

    private async Task OnChange(ChangeEventArgs e) {
        Value = e.Value?.ToString() ?? "";
        await ValueChanged.InvokeAsync(Value);
        Form?.MarkDirty();
    }
}
