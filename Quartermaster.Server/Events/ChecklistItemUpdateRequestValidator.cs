using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Events;

public class ChecklistItemUpdateRequestValidator : Validator<ChecklistItemUpdateRequest> {
    public ChecklistItemUpdateRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Event.Checklist.EventRequired);

        RuleFor(x => x.ItemId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Event.Checklist.ItemIdRequired);

        RuleFor(x => x.Label)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Event.Checklist.LabelRequired)
            .MaximumLength(1024)
            .WithMessage(I18nKey.Error.Event.Checklist.LabelMaxLength);

        RuleFor(x => x.ItemType)
            .InclusiveBetween(0, 2)
            .WithMessage(I18nKey.Error.Event.Checklist.TypeInvalid);
    }
}
