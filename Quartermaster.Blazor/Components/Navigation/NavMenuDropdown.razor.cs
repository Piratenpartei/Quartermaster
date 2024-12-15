using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Navigation;

public partial class NavMenuDropdown {
    private bool Collapsed = true;

    [Parameter]
    public required RenderFragment ButtonContent { get; set; }
    [Parameter]
    public required RenderFragment DropdownContent { get; set; }

    private void ToggleDropdown() => Collapsed = !Collapsed;

    private async Task OnFocusOut() {
        // Without a short delay the menu would close and the link would not be clicked ... because it would already be gone.
        // We can replace this with the new popover api (good support) + css anchoring (basically no support atm)
        // as soon as the support is good enough for both.
        // NOTE: This isn't even working correct and needs a refactor, clicks > 100ms fail to navigate.
        await Task.Delay(100);
        Collapsed = true;
    }
}