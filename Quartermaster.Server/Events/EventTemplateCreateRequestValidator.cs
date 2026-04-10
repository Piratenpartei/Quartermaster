using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Events;

public class EventTemplateCreateRequestValidator : Validator<EventTemplateCreateRequest> {
    public EventTemplateCreateRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Event.Template.EventRequired);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Event.Template.NameRequired)
            .MaximumLength(512)
            .WithMessage(I18nKey.Error.Event.Template.NameMaxLength);
    }
}
