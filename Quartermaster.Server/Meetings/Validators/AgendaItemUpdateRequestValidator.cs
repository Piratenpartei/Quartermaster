using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemUpdateRequestValidator : Validator<AgendaItemUpdateRequest> {
    public AgendaItemUpdateRequestValidator() {
        RuleFor(x => x.MeetingId)
            .NotEqual(Guid.Empty)
            .WithMessage("Sitzung muss angegeben werden.");

        RuleFor(x => x.ItemId)
            .NotEqual(Guid.Empty)
            .WithMessage("Tagesordnungspunkt muss angegeben werden.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Titel darf nicht leer sein.")
            .MaximumLength(200)
            .WithMessage("Titel darf maximal 200 Zeichen lang sein.");

        RuleFor(x => x.ItemType)
            .IsInEnum()
            .WithMessage("Ungültiger Tagesordnungspunkt-Typ.");

        RuleFor(x => x.Notes)
            .MaximumLength(20000)
            .WithMessage("Notizen dürfen maximal 20000 Zeichen lang sein.");

        RuleFor(x => x.Resolution)
            .MaximumLength(5000)
            .WithMessage("Beschluss darf maximal 5000 Zeichen lang sein.");
    }
}
