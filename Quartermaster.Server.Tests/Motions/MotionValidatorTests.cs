using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Motions;

namespace Quartermaster.Server.Tests.Motions;

public class MotionCreateRequestValidatorTests {
    private readonly MotionCreateRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = "max@example.com",
            Title = "Testantrag",
            Text = "Das ist ein Antragstext."
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyChapterId_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.Empty,
            AuthorName = "Max Mustermann",
            AuthorEmail = "max@example.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorMessage(I18nKey.Error.Motion.ChapterRequired);
    }

    [Test]
    public void EmptyAuthorName_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "",
            AuthorEmail = "max@example.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorName)
            .WithErrorMessage(I18nKey.Error.Motion.SubmitterNameRequired);
    }

    [Test]
    public void AuthorNameExceedsMaxLength_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = new string('A', 257),
            AuthorEmail = "max@example.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorName)
            .WithErrorMessage(I18nKey.Error.Motion.SubmitterNameMaxLength);
    }

    [Test]
    public void AuthorNameAtMaxLength_ShouldHaveNoError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = new string('A', 256),
            AuthorEmail = "max@example.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.AuthorName);
    }

    [Test]
    public void EmptyAuthorEmail_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = "",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorEmail)
            .WithErrorMessage(I18nKey.Error.Motion.EmailRequired);
    }

    [Test]
    public void AuthorEmailWithoutAt_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = "maxexample.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorEmail)
            .WithErrorMessage(I18nKey.Error.Motion.EmailInvalid);
    }

    [Test]
    public void AuthorEmailExceedsMaxLength_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = new string('a', 251) + "@ab.de",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorEmail)
            .WithErrorMessage(I18nKey.Error.Motion.EmailMaxLength);
    }

    [Test]
    public void AuthorEmailAtMaxLength_ShouldHaveNoError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = new string('a', 249) + "@ab.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.AuthorEmail);
    }

    [Test]
    public void EmptyTitle_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = "max@example.com",
            Title = "",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(I18nKey.Error.Motion.TitleRequired);
    }

    [Test]
    public void TitleExceedsMaxLength_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = "max@example.com",
            Title = new string('A', 513),
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(I18nKey.Error.Motion.TitleMaxLength);
    }

    [Test]
    public void TitleAtMaxLength_ShouldHaveNoError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = "max@example.com",
            Title = new string('A', 512),
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Test]
    public void EmptyText_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = "max@example.com",
            Title = "Testantrag",
            Text = ""
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(I18nKey.Error.Motion.BodyRequired);
    }

    [Test]
    public void TextExceedsMaxLength_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = "max@example.com",
            Title = "Testantrag",
            Text = new string('A', 8193)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(I18nKey.Error.Motion.BodyMaxLength);
    }

    [Test]
    public void TextAtMaxLength_ShouldHaveNoError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEmail = "max@example.com",
            Title = "Testantrag",
            Text = new string('A', 8192)
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Text);
    }
}

public class MotionStatusRequestValidatorTests {
    private readonly MotionStatusRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new MotionStatusRequest {
            MotionId = Guid.NewGuid(),
            ApprovalStatus = MotionApprovalStatus.Approved,
            IsRealized = true
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequestWithNulls_ShouldHaveNoErrors() {
        var request = new MotionStatusRequest {
            MotionId = Guid.NewGuid(),
            ApprovalStatus = null,
            IsRealized = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyMotionId_ShouldHaveError() {
        var request = new MotionStatusRequest {
            MotionId = Guid.Empty,
            ApprovalStatus = MotionApprovalStatus.Approved
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.MotionId)
            .WithErrorMessage(I18nKey.Error.Motion.Status.MotionIdRequired);
    }
}

public class MotionVoteRequestValidatorTests {
    private readonly MotionVoteRequestValidator _validator = new();

    [Test]
    public void ValidRequest_Approve_ShouldHaveNoErrors() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = VoteType.Approve
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequest_Deny_ShouldHaveNoErrors() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = VoteType.Deny
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequest_Abstain_ShouldHaveNoErrors() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = VoteType.Abstain
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyMotionId_ShouldHaveError() {
        var request = new MotionVoteRequest {
            MotionId = Guid.Empty,
            UserId = Guid.NewGuid(),
            Vote = VoteType.Approve
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.MotionId)
            .WithErrorMessage(I18nKey.Error.Motion.Vote.MotionIdRequired);
    }

    [Test]
    public void EmptyUserId_ShouldHaveError() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.Empty,
            Vote = VoteType.Approve
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage(I18nKey.Error.Motion.Vote.UserIdRequired);
    }

    [Test]
    public void VoteBelowRange_ShouldHaveError() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = (VoteType)(-1)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Vote)
            .WithErrorMessage(I18nKey.Error.Motion.Vote.InvalidVote);
    }

    [Test]
    public void VoteAboveRange_ShouldHaveError() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = (VoteType)3
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Vote)
            .WithErrorMessage(I18nKey.Error.Motion.Vote.InvalidVote);
    }
}
