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

    private void Close() => Collapsed = true;

    /// <summary>Closes the menu when the keyboard focus leaves it. Safari never focuses a clicked button, so mouse and touch users rely on the backdrop instead.</summary>
    private async Task OnFocusOut(FocusEventArgs e) {
        // Delay to allow click events on dropdown items to fire before the menu closes.
        // 200ms is enough for most click interactions.
        await Task.Delay(200);
        Collapsed = true;
    }
}
