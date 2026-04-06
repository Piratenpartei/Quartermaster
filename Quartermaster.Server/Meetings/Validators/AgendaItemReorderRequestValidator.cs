using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemReorderRequestValidator : Validator<AgendaItemReorderRequest> {
    public AgendaItemReorderRequestValidator() {
        RuleFor(x => x.MeetingId)
            .NotEqual(Guid.Empty)
            .WithMessage("Sitzung muss angegeben werden.");

        RuleFor(x => x.ItemId)
            .NotEqual(Guid.Empty)
            .WithMessage("Tagesordnungspunkt muss angegeben werden.");

        RuleFor(x => x.Direction)
            .Must(d => d == -1 || d == 1)
            .WithMessage("Richtung muss -1 oder +1 sein.");
    }
}
