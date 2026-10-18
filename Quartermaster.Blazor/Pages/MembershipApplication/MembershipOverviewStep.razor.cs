using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Pages.DueSelector;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class MembershipOverviewStep {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public MembershipApplicationEntryState State { get; set; } = default!;

    [Parameter, EditorRequired]
    public DueSelectorEntryState DuesState { get; set; } = default!;

    private DateOnly? DateOfBirth() {
        return State.DateOfBirth == null ? null : DateOnly.FromDateTime(State.DateOfBirth.Value);
    }

    private string ConformityStatus() {
        return State.ConformityDeclarationAccepted
            ? I18n[I18nKey.Ui.ApplicationSummary.ConformityAccepted]
            : I18n[I18nKey.Ui.ApplicationSummary.ConformityNotAccepted];
    }
}
