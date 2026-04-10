using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Events;

public class EventCreateRequestValidator : Validator<EventCreateRequest> {
    public EventCreateRequestValidator() {
        RuleFor(x => x.ChapterId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Event.ChapterRequired);

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
