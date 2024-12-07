using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Navigation;

public partial class NavMenuDropdown {
    private bool Collapsed = true;

    [Parameter]
    public required RenderFragment ButtonContent { get; set; }
    [Parameter]
    public required RenderFragment DropdownContent { get; set; }

    private void ToggleDropdown() {
        Collapsed = !Collapsed;
    }
}