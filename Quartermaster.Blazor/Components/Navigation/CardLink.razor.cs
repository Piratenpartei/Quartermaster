using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Navigation;

public partial class CardLink {
    [Parameter]
    public required RenderFragment ChildContent { get; set; }
    
    [Parameter]
    public required string HRef { get; set; }
    [Parameter]
    public EventCallback OnNavigate { get; set; }

    [Parameter]
    public bool Enabled { get; set; } = true;
    [Parameter]
    public Func<bool> EnabledFunc { get; set; }

    [Parameter]
    public bool Hovered { get; set; }
    [Parameter]
    public EventCallback<bool> HoveredChanged { get; set; }

    private bool PointerInsideCard;

    public CardLink() {
        EnabledFunc = IsEnabled;
    }

    private bool IsEnabled() => Enabled;

    private void OnPointerEnter() {
        PointerInsideCard = true;
        HoveredChanged.InvokeAsync(PointerInsideCard);
    }

    private void OnPointerLeave() {
        PointerInsideCard = false;
        HoveredChanged.InvokeAsync(PointerInsideCard);
    }
}