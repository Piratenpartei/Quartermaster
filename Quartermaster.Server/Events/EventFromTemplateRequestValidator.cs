using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Events;

public class EventFromTemplateRequestValidator : Validator<EventFromTemplateRequest> {
    public EventFromTemplateRequestValidator() {
        RuleFor(x => x.TemplateId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Event.Template.TemplateRequired);

        RuleFor(x => x.ChapterId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Event.Template.ChapterRequired);
    }
}
