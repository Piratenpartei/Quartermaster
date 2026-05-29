using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class AgendaItemVoteRequestValidator : Validator<AgendaItemVoteRequest> {
    public AgendaItemVoteRequestValidator() {
        RuleFor(x => x.MeetingId).NotEqual(System.Guid.Empty);
        RuleFor(x => x.ItemId).NotEqual(System.Guid.Empty);
        RuleFor(x => x.MemberId).NotEqual(System.Guid.Empty);
        RuleFor(x => x.Vote).IsInEnum()
            .WithMessage(I18nKey.Error.Meeting.Agenda.VoteValueInvalid);
    }
}
