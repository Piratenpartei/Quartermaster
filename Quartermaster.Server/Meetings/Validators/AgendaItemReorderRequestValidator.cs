using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemReorderRequestValidator : Validator<AgendaItemReorderRequest> {
    public AgendaItemReorderRequestValidator() {
        RuleFor(x => x.MeetingId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.Meeting.Agenda.MeetingRequired);

        RuleFor(x => x.ItemId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.Meeting.Agenda.ItemRequired);

        RuleFor(x => x.Direction)
            .Must(d => d == -1 || d == 1)
            .WithMessage(I18nKey.Error.Meeting.Agenda.ReorderDirectionInvalid);
    }
}
