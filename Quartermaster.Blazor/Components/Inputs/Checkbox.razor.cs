using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class Checkbox {
    [Parameter]
    public bool Value { get; set; }
    [Parameter]
    public EventCallback<bool> ValueChanged { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private void ToggleState() {
        Value = !Value;
        ValueChanged.InvokeAsync(Value);
    }
}