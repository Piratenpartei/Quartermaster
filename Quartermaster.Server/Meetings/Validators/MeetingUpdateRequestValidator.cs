using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class MeetingUpdateRequestValidator : Validator<MeetingUpdateRequest> {
    public MeetingUpdateRequestValidator() {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Meeting.TitleRequired)
            .MaximumLength(200)
            .WithMessage(I18nKey.Error.Meeting.TitleMaxLength);

        RuleFor(x => x.Location)
            .MaximumLength(500)
            .WithMessage(I18nKey.Error.Meeting.LocationMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(10000)
            .WithMessage(I18nKey.Error.Meeting.DescriptionMaxLength);

        RuleFor(x => x.Visibility)
            .IsInEnum()
            .WithMessage(I18nKey.Error.Meeting.VisibilityInvalid);
    }
}
