using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class LoadingSpinner {
    /// <summary>
    /// Use the small spinner variant. Default is the regular size.
    /// </summary>
    [Parameter]
    public bool Small { get; set; }

    /// <summary>
    /// Extra CSS classes for the wrapper. Defaults to "my-4" for the standard
    /// "loading a page" usage. Pass an empty string for inline use.
    /// </summary>
    [Parameter]
    public string CssClass { get; set; } = "my-4";
}
