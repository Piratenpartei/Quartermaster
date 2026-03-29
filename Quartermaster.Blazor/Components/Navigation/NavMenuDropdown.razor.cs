using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Quartermaster.Blazor.Components.Navigation;

public partial class NavMenuDropdown {
    private bool Collapsed = true;

    [Parameter]
    public required RenderFragment ButtonContent { get; set; }
    [Parameter]
    public required RenderFragment DropdownContent { get; set; }

    private void ToggleDropdown() => Collapsed = !Collapsed;

    private async Task OnFocusOut(FocusEventArgs e) {
        // Delay to allow click events on dropdown items to fire before the menu closes.
        // 200ms is enough for most click interactions.
        await Task.Delay(200);
        Collapsed = true;
    }
}
