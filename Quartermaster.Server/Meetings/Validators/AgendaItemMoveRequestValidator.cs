using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemMoveRequestValidator : Validator<AgendaItemMoveRequest> {
    public AgendaItemMoveRequestValidator() {
        RuleFor(x => x.MeetingId)
            .NotEqual(Guid.Empty)
            .WithMessage("Sitzung muss angegeben werden.");

        RuleFor(x => x.ItemId)
            .NotEqual(Guid.Empty)
            .WithMessage("Tagesordnungspunkt muss angegeben werden.");
    }
}
