using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class RadioGroup<E> where E : Enum {
    [Parameter]
    public E Value { get; set; } = default!;
    [Parameter]
    public EventCallback<E> ValueChanged { get; set; }

    [Parameter]
    public Func<E, string>? ToStringFunc { get; set; }

    private void OnChange(ChangeEventArgs args) {
        if (args.Value == null)
            return;

        if (Enum.TryParse(typeof(E), args.Value.ToString(), out var enumValue)) {
            Value = (E)enumValue;
            ValueChanged.InvokeAsync(Value);
        }
    }
}