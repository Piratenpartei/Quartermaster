using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Layout;

public partial class MainFooter {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public bool Fluid { get; set; }
}
