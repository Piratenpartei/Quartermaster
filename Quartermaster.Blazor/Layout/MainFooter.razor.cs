using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Layout;

public partial class MainFooter {
    private const string RepositoryUrl = "https://github.com/Piratenpartei/Quartermaster";

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public bool Fluid { get; set; }
}
