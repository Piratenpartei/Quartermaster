using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Navigation; 

public partial class CardButton {
    [Parameter]
    public required RenderFragment ChildContent { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    [Parameter]
    public bool Enabled { get; set; } = true;
    [Parameter]
    public Func<bool> EnabledFunc { get; set; }

    [Parameter]
    public bool Hovered { get; set; }
    [Parameter]
    public EventCallback<bool> HoveredChanged { get; set; }

    private bool PointerInsideCard;

    public CardButton() {
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