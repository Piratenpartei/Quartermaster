using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Inputs;

/// <summary>A card that works as one radio button: the whole card selects, and cards sharing a <see cref="Name"/> form a group the arrow keys move through.</summary>
public partial class ChoiceCard {
    [Parameter, EditorRequired]
    public string Name { get; set; } = "";

    [Parameter]
    public bool Selected { get; set; }

    [Parameter]
    public EventCallback OnSelect { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string CardClass() {
        return Selected ? "border-primary bg-primary-subtle" : "hover-interact qm-bg-body qm-hover-bg-secondary";
    }

    private Task OnChange() {
        return OnSelect.InvokeAsync();
    }
}
