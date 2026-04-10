using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Events;

public class ChecklistItemReorderRequestValidator : Validator<ChecklistItemReorderRequest> {
    public ChecklistItemReorderRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Event.Checklist.EventRequired);

        RuleFor(x => x.ItemId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Event.Checklist.ItemIdRequired);

        RuleFor(x => x.Direction)
            .Must(d => d == -1 || d == 1)
            .WithMessage(I18nKey.Error.Event.Checklist.ReorderDirectionInvalid);
    }
}
