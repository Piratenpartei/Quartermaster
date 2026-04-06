using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemNotesRequestValidator : Validator<AgendaItemNotesRequest> {
    public AgendaItemNotesRequestValidator() {
        RuleFor(x => x.MeetingId)
            .NotEqual(Guid.Empty)
            .WithMessage("Sitzung muss angegeben werden.");

        RuleFor(x => x.ItemId)
            .NotEqual(Guid.Empty)
            .WithMessage("Tagesordnungspunkt muss angegeben werden.");

        RuleFor(x => x.Notes)
            .MaximumLength(20000)
            .WithMessage("Notizen dürfen maximal 20000 Zeichen lang sein.");
    }
}
