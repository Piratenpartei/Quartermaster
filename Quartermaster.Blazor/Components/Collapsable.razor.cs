using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class Collapsable {
    [Parameter]
    public bool Collapsed { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}