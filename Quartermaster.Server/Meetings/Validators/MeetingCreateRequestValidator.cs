using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class MeetingCreateRequestValidator : Validator<MeetingCreateRequest> {
    public MeetingCreateRequestValidator() {
        RuleFor(x => x.ChapterId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.Meeting.ChapterRequired);

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
