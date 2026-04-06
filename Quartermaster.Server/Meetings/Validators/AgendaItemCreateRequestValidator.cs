using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemCreateRequestValidator : Validator<AgendaItemCreateRequest> {
    public AgendaItemCreateRequestValidator() {
        RuleFor(x => x.MeetingId)
            .NotEqual(Guid.Empty)
            .WithMessage("Sitzung muss angegeben werden.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Titel darf nicht leer sein.")
            .MaximumLength(200)
            .WithMessage("Titel darf maximal 200 Zeichen lang sein.");

        RuleFor(x => x.ItemType)
            .IsInEnum()
            .WithMessage("Ungültiger Tagesordnungspunkt-Typ.");
    }
}
