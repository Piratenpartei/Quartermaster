using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionPortalUpdateRequestValidator : Validator<AssemblyMotionPortalUpdateRequest> {
    public AssemblyMotionPortalUpdateRequestValidator() {
        RuleFor(x => x.ClosesAt)
            .GreaterThan(x => x.OpensAt)
            .WithMessage(I18nKey.Error.AssemblyMotion.Portal.ClosesBeforeOpens);

        When(x => x.PrioritizationEnabled, () => {
            RuleFor(x => x.PrioritizationStartsAt)
                .NotNull()
                .WithMessage(I18nKey.Error.AssemblyMotion.Portal.PrioritizationWindowRequired);

            RuleFor(x => x.PrioritizationEndsAt)
                .NotNull()
                .WithMessage(I18nKey.Error.AssemblyMotion.Portal.PrioritizationWindowRequired);

            When(x => x.PrioritizationStartsAt != null && x.PrioritizationEndsAt != null, () => {
                RuleFor(x => x.PrioritizationEndsAt)
                    .GreaterThan(x => x.PrioritizationStartsAt)
                    .WithMessage(I18nKey.Error.AssemblyMotion.Portal.PrioritizationWindowInvalid);

                RuleFor(x => x.PrioritizationEndsAt)
                    .LessThanOrEqualTo(x => x.ClosesAt)
                    .WithMessage(I18nKey.Error.AssemblyMotion.Portal.PrioritizationEndAfterClose);
            });
        });

        When(x => !string.IsNullOrEmpty(x.WikiPagePrefix), () => {
            RuleFor(x => x.WikiPagePrefix)
                .Must(AssemblyMotionWikiPage.IsValidPrefix)
                .WithMessage(I18nKey.Error.AssemblyMotion.WikiPrefixInvalid);
        });

        When(x => x.WikiMirrorEnabled, () => {
            RuleFor(x => x.WikiPagePrefix)
                .NotEmpty()
                .WithMessage(I18nKey.Error.AssemblyMotion.Portal.WikiPagePrefixRequired);

            RuleFor(x => x.WikiAssemblyLabel)
                .NotEmpty()
                .WithMessage(I18nKey.Error.AssemblyMotion.Portal.WikiAssemblyLabelRequired);
        });
    }
}
