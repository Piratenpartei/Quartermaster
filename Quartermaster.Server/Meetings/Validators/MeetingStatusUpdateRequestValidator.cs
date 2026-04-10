using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class MeetingStatusUpdateRequestValidator : Validator<MeetingStatusUpdateRequest> {
    public MeetingStatusUpdateRequestValidator() {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage(I18nKey.Error.Meeting.Status.Invalid);
    }
}
