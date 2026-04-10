using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Events;

public class EventUpdateRequestValidator : Validator<EventUpdateRequest> {
    public EventUpdateRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Event.IdRequired);

        RuleFor(x => x.InternalName)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Event.InternalNameRequired)
            .MaximumLength(512)
            .WithMessage(I18nKey.Error.Event.InternalNameMaxLength);

        RuleFor(x => x.PublicName)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Event.PublicNameRequired)
            .MaximumLength(512)
            .WithMessage(I18nKey.Error.Event.PublicNameMaxLength);
    }
}
