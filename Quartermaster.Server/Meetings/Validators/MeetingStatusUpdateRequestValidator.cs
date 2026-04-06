using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class MeetingStatusUpdateRequestValidator : Validator<MeetingStatusUpdateRequest> {
    public MeetingStatusUpdateRequestValidator() {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Ungültiger Sitzungsstatus.");
    }
}
