using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemUpdateRequestValidator : Validator<AgendaItemUpdateRequest> {
    public AgendaItemUpdateRequestValidator() {
        RuleFor(x => x.MeetingId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.Meeting.Agenda.MeetingRequired);

        RuleFor(x => x.ItemId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.Meeting.Agenda.ItemRequired);

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Meeting.Agenda.TitleRequired)
            .MaximumLength(200)
            .WithMessage(I18nKey.Error.Meeting.Agenda.TitleMaxLength);

        RuleFor(x => x.ItemType)
            .IsInEnum()
            .WithMessage(I18nKey.Error.Meeting.Agenda.ItemTypeInvalid);

        RuleFor(x => x.Notes)
            .MaximumLength(20000)
            .WithMessage(I18nKey.Error.Meeting.Agenda.NotesMaxLength);

        RuleFor(x => x.Resolution)
            .MaximumLength(5000)
            .WithMessage(I18nKey.Error.Meeting.Agenda.ResolutionMaxLength);
    }
}
