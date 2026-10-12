using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionContentCard {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required AssemblyMotionPublicDTO Motion { get; set; }

    [Parameter]
    public bool ShowCompetitions { get; set; } = true;
}
