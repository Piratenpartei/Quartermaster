using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class MembershipDeclarationsStep {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public MembershipApplicationEntryState State { get; set; } = default!;

    // The form asks the positive question, the application stores the negative one.
    private bool NoPriorDeclinedApplication {
        get => !State.HasPriorDeclinedApplication;
        set => State.HasPriorDeclinedApplication = !value;
    }
}
