using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class ChapterMarkBadges {
    [Parameter]
    public bool NoMemberAdministration { get; set; }

    [Parameter]
    public bool UnableToAct { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }
}
