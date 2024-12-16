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

    public CardLink() {
        EnabledFunc = IsEnabled;
    }

    private bool IsEnabled() => Enabled;
}