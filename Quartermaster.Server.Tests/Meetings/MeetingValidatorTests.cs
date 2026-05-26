using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Meetings.Validators;

namespace Quartermaster.Server.Tests.Meetings;

public class MeetingCreateRequestValidatorTests {
    private readonly MeetingCreateRequestValidator _validator = new();

    private static MeetingCreateRequest Valid() => new() {
        ChapterId = Guid.NewGuid(),
        Title = "Sitzung",
        Visibility = MeetingVisibility.Private
    };

    [Test]
    public void Valid_request_has_no_errors() {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Empty_chapter_id_errors() {
        var req = Valid();
        req.ChapterId = Guid.Empty;
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorMessage(I18nKey.Error.Meeting.ChapterRequired);
    }

    [Test]
    public void Empty_title_errors() {
        var req = Valid();
        req.Title = "";
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(I18nKey.Error.Meeting.TitleRequired);
    }

    [Test]
    public void Title_at_201_chars_errors() {
        var req = Valid();
        req.Title = new string('a', 201);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(I18nKey.Error.Meeting.TitleMaxLength);
    }

    [Test]
    public void Location_at_501_chars_errors() {
        var req = Valid();
        req.Location = new string('l', 501);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Location)
            .WithErrorMessage(I18nKey.Error.Meeting.LocationMaxLength);
    }

    [Test]
    public void Description_at_10001_chars_errors() {
        var req = Valid();
        req.Description = new string('d', 10001);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage(I18nKey.Error.Meeting.DescriptionMaxLength);
    }

    [Test]
    public void Invalid_visibility_errors() {
        var req = Valid();
        req.Visibility = (MeetingVisibility)999;
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Visibility)
            .WithErrorMessage(I18nKey.Error.Meeting.VisibilityInvalid);
    }
}

public class MeetingUpdateRequestValidatorTests {
    private readonly MeetingUpdateRequestValidator _validator = new();

    private static MeetingUpdateRequest Valid() => new() {
        Id = Guid.NewGuid(),
        Title = "Sitzung",
        Visibility = MeetingVisibility.Private
    };

    [Test]
    public void Valid_request_has_no_errors() {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Empty_title_errors() {
        var req = Valid();
        req.Title = "";
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Test]
    public void Long_location_errors() {
        var req = Valid();
        req.Location = new string('a', 501);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Location);
    }

    [Test]
    public void Invalid_visibility_errors() {
        var req = Valid();
        req.Visibility = (MeetingVisibility)42;
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Visibility);
    }
}

public class MeetingStatusUpdateRequestValidatorTests {
    private readonly MeetingStatusUpdateRequestValidator _validator = new();

    [Test]
    public void Valid_status_has_no_errors() {
        var result = _validator.TestValidate(new MeetingStatusUpdateRequest { Status = MeetingStatus.InProgress });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Invalid_status_errors() {
        var result = _validator.TestValidate(new MeetingStatusUpdateRequest { Status = (MeetingStatus)999 });
        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage(I18nKey.Error.Meeting.Status.Invalid);
    }
}

public class MeetingListRequestValidatorTests {
    private readonly MeetingListRequestValidator _validator = new();

    [Test]
    public void Defaults_pass() {
        var result = _validator.TestValidate(new MeetingListRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Page_zero_errors() {
        var result = _validator.TestValidate(new MeetingListRequest { Page = 0, PageSize = 25 });
        result.ShouldHaveValidationErrorFor(x => x.Page)
            .WithErrorMessage(I18nKey.Error.Validation.PageMin);
    }

    [Test]
    public void PageSize_zero_errors() {
        var result = _validator.TestValidate(new MeetingListRequest { Page = 1, PageSize = 0 });
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage(I18nKey.Error.Validation.PageSizeRange);
    }

    [Test]
    public void PageSize_101_errors() {
        var result = _validator.TestValidate(new MeetingListRequest { Page = 1, PageSize = 101 });
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage(I18nKey.Error.Validation.PageSizeRange);
    }
}
