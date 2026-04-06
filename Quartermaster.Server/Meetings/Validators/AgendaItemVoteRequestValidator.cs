using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemVoteRequestValidator : Validator<AgendaItemVoteRequest> {
    public AgendaItemVoteRequestValidator() {
        RuleFor(x => x.MeetingId).NotEqual(System.Guid.Empty);
        RuleFor(x => x.ItemId).NotEqual(System.Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(System.Guid.Empty);
        RuleFor(x => x.Vote).InclusiveBetween(0, 2)
            .WithMessage("Vote muss 0 (Ja), 1 (Nein) oder 2 (Enthaltung) sein.");
    }
}
