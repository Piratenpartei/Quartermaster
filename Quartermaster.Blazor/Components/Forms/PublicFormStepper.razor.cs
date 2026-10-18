using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Pages.MembershipApplication;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Forms;

public enum PublicFormStep {
    PersonalData,
    Address,
    Dues,
    Payment,
    Declarations,
    Summary
}

/// <summary>The step breadcrumb of the membership application and the due selection, one page per step.</summary>
public partial class PublicFormStepper {
    private static readonly PublicFormStep[] MembershipApplicationSteps = [
        PublicFormStep.PersonalData, PublicFormStep.Address, PublicFormStep.Dues, PublicFormStep.Payment,
        PublicFormStep.Declarations, PublicFormStep.Summary
    ];

    private static readonly PublicFormStep[] DueSelectionSteps = [
        PublicFormStep.PersonalData, PublicFormStep.Dues, PublicFormStep.Payment, PublicFormStep.Summary
    ];

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required AppStateService AppState { get; set; }

    [Parameter, EditorRequired]
    public PublicFormStep Step { get; set; }

    /// <summary>
    /// Left out on the dues and payment pages both forms share. They count as part of the membership application while
    /// one is in progress, the same rule the due selection summary uses to hand over to the declarations.
    /// </summary>
    [Parameter]
    public PublicFormKind? Form { get; set; }

    private List<FormStep> Steps = [];
    private int Current;

    protected override void OnParametersSet() {
        var form = Form ?? DetectForm();
        var steps = form == PublicFormKind.MembershipApplication ? MembershipApplicationSteps : DueSelectionSteps;
        Steps = steps
            .Select(s => new FormStep(I18n[TitleKey(s)], Href(form, s)))
            .ToList();
        Current = Math.Max(0, Array.IndexOf(steps, Step));
    }

    private PublicFormKind DetectForm() {
        var membership = AppState.GetEntryState<MembershipApplicationEntryState>();
        return string.IsNullOrEmpty(membership.FirstName) ? PublicFormKind.DueSelection : PublicFormKind.MembershipApplication;
    }

    // The payment page needs the dues page it came from, and the summary is never behind the current step, so neither links.
    private static string? Href(PublicFormKind form, PublicFormStep step) {
        var membership = form == PublicFormKind.MembershipApplication;
        return step switch {
            PublicFormStep.PersonalData => membership ? "/MembershipApplication/PersonalData" : "/DueSelector/UserDataInput",
            PublicFormStep.Address => "/MembershipApplication/CountrySelection",
            PublicFormStep.Dues => membership ? "/MembershipApplication/DuesTypeSelection" : "/DueSelector/DueTypeSelector",
            PublicFormStep.Declarations => "/MembershipApplication/Declarations",
            _ => null
        };
    }

    private static string TitleKey(PublicFormStep step) {
        return step switch {
            PublicFormStep.PersonalData => I18nKey.Ui.PublicFormStepper.PersonalData,
            PublicFormStep.Address => I18nKey.Ui.PublicFormStepper.Address,
            PublicFormStep.Dues => I18nKey.Ui.PublicFormStepper.Dues,
            PublicFormStep.Payment => I18nKey.Ui.PublicFormStepper.Payment,
            PublicFormStep.Declarations => I18nKey.Ui.PublicFormStepper.Declarations,
            _ => I18nKey.Ui.PublicFormStepper.Summary
        };
    }
}
