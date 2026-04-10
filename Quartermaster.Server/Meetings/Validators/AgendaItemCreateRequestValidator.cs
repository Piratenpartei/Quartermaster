using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemCreateRequestValidator : Validator<AgendaItemCreateRequest> {
    public AgendaItemCreateRequestValidator() {
        RuleFor(x => x.MeetingId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.Meeting.Agenda.MeetingRequired);

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Meeting.Agenda.TitleRequired)
            .MaximumLength(200)
            .WithMessage(I18nKey.Error.Meeting.Agenda.TitleMaxLength);

        RuleFor(x => x.ItemType)
            .IsInEnum()
            .WithMessage(I18nKey.Error.Meeting.Agenda.ItemTypeInvalid);
    }
}
