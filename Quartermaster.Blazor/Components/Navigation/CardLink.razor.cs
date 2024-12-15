using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Navigation;

public partial class CardLink {
    [Parameter]
    public required RenderFragment ChildContent { get; set; }
    [Parameter]
    public required string HRef { get; set; }
}