using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemNotesRequestValidator : Validator<AgendaItemNotesRequest> {
    public AgendaItemNotesRequestValidator() {
        RuleFor(x => x.MeetingId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.Meeting.Agenda.MeetingRequired);

        RuleFor(x => x.ItemId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.Meeting.Agenda.ItemRequired);

        RuleFor(x => x.Notes)
            .MaximumLength(20000)
            .WithMessage(I18nKey.Error.Meeting.Agenda.NotesMaxLength);
    }
}
