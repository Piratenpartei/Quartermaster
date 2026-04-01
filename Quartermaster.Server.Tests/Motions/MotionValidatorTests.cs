using System;
using FluentValidation.TestHelper;
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
            AuthorEMail = "max@example.com",
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
            AuthorEMail = "max@example.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorMessage("Gliederung muss ausgewählt werden.");
    }

    [Test]
    public void EmptyAuthorName_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "",
            AuthorEMail = "max@example.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorName)
            .WithErrorMessage("Name des Antragstellers darf nicht leer sein.");
    }

    [Test]
    public void AuthorNameExceedsMaxLength_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = new string('A', 257),
            AuthorEMail = "max@example.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorName)
            .WithErrorMessage("Name des Antragstellers darf maximal 256 Zeichen lang sein.");
    }

    [Test]
    public void AuthorNameAtMaxLength_ShouldHaveNoError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = new string('A', 256),
            AuthorEMail = "max@example.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.AuthorName);
    }

    [Test]
    public void EmptyAuthorEMail_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = "",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorEMail)
            .WithErrorMessage("E-Mail-Adresse darf nicht leer sein.");
    }

    [Test]
    public void AuthorEMailWithoutAt_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = "maxexample.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorEMail)
            .WithErrorMessage("E-Mail-Adresse muss ein @ enthalten.");
    }

    [Test]
    public void AuthorEMailExceedsMaxLength_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = new string('a', 251) + "@ab.de",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AuthorEMail)
            .WithErrorMessage("E-Mail-Adresse darf maximal 256 Zeichen lang sein.");
    }

    [Test]
    public void AuthorEMailAtMaxLength_ShouldHaveNoError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = new string('a', 249) + "@ab.com",
            Title = "Testantrag",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.AuthorEMail);
    }

    [Test]
    public void EmptyTitle_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = "max@example.com",
            Title = "",
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Titel darf nicht leer sein.");
    }

    [Test]
    public void TitleExceedsMaxLength_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = "max@example.com",
            Title = new string('A', 513),
            Text = "Text"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Titel darf maximal 512 Zeichen lang sein.");
    }

    [Test]
    public void TitleAtMaxLength_ShouldHaveNoError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = "max@example.com",
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
            AuthorEMail = "max@example.com",
            Title = "Testantrag",
            Text = ""
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage("Antragstext darf nicht leer sein.");
    }

    [Test]
    public void TextExceedsMaxLength_ShouldHaveError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = "max@example.com",
            Title = "Testantrag",
            Text = new string('A', 8193)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage("Antragstext darf maximal 8192 Zeichen lang sein.");
    }

    [Test]
    public void TextAtMaxLength_ShouldHaveNoError() {
        var request = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = "max@example.com",
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
            ApprovalStatus = 1,
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
            ApprovalStatus = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.MotionId)
            .WithErrorMessage("Antrags-ID darf nicht leer sein.");
    }
}

public class MotionVoteRequestValidatorTests {
    private readonly MotionVoteRequestValidator _validator = new();

    [Test]
    public void ValidRequest_Approve_ShouldHaveNoErrors() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequest_Deny_ShouldHaveNoErrors() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequest_Abstain_ShouldHaveNoErrors() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = 2
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyMotionId_ShouldHaveError() {
        var request = new MotionVoteRequest {
            MotionId = Guid.Empty,
            UserId = Guid.NewGuid(),
            Vote = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.MotionId)
            .WithErrorMessage("Antrags-ID darf nicht leer sein.");
    }

    [Test]
    public void EmptyUserId_ShouldHaveError() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.Empty,
            Vote = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("Benutzer-ID darf nicht leer sein.");
    }

    [Test]
    public void VoteBelowRange_ShouldHaveError() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = -1
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Vote)
            .WithErrorMessage("Ungültige Abstimmung.");
    }

    [Test]
    public void VoteAboveRange_ShouldHaveError() {
        var request = new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Vote = 3
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Vote)
            .WithErrorMessage("Ungültige Abstimmung.");
    }
}
