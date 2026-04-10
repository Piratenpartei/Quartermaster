using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Server.Events;

namespace Quartermaster.Server.Tests.Events;

public class EventCreateRequestValidatorTests {
    private readonly EventCreateRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = "Test Event",
            PublicName = "Public Event"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyChapterId_ShouldHaveError() {
        var request = new EventCreateRequest {
            ChapterId = Guid.Empty,
            InternalName = "Test",
            PublicName = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorMessage(I18nKey.Error.Event.ChapterRequired);
    }

    [Test]
    public void EmptyInternalName_ShouldHaveError() {
        var request = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = "",
            PublicName = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.InternalName)
            .WithErrorMessage(I18nKey.Error.Event.InternalNameRequired);
    }

    [Test]
    public void InternalNameExceedsMaxLength_ShouldHaveError() {
        var request = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = new string('A', 513),
            PublicName = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.InternalName)
            .WithErrorMessage(I18nKey.Error.Event.InternalNameMaxLength);
    }

    [Test]
    public void InternalNameAtMaxLength_ShouldHaveNoError() {
        var request = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = new string('A', 512),
            PublicName = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.InternalName);
    }

    [Test]
    public void EmptyPublicName_ShouldHaveError() {
        var request = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = "Test",
            PublicName = ""
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PublicName)
            .WithErrorMessage(I18nKey.Error.Event.PublicNameRequired);
    }

    [Test]
    public void PublicNameExceedsMaxLength_ShouldHaveError() {
        var request = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = "Test",
            PublicName = new string('A', 513)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PublicName)
            .WithErrorMessage(I18nKey.Error.Event.PublicNameMaxLength);
    }

    [Test]
    public void PublicNameAtMaxLength_ShouldHaveNoError() {
        var request = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = "Test",
            PublicName = new string('A', 512)
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.PublicName);
    }
}

public class EventUpdateRequestValidatorTests {
    private readonly EventUpdateRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new EventUpdateRequest {
            Id = Guid.NewGuid(),
            InternalName = "Test Event",
            PublicName = "Public Event"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyId_ShouldHaveError() {
        var request = new EventUpdateRequest {
            Id = Guid.Empty,
            InternalName = "Test",
            PublicName = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage(I18nKey.Error.Event.IdRequired);
    }

    [Test]
    public void EmptyInternalName_ShouldHaveError() {
        var request = new EventUpdateRequest {
            Id = Guid.NewGuid(),
            InternalName = "",
            PublicName = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.InternalName)
            .WithErrorMessage(I18nKey.Error.Event.InternalNameRequired);
    }

    [Test]
    public void InternalNameExceedsMaxLength_ShouldHaveError() {
        var request = new EventUpdateRequest {
            Id = Guid.NewGuid(),
            InternalName = new string('A', 513),
            PublicName = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.InternalName)
            .WithErrorMessage(I18nKey.Error.Event.InternalNameMaxLength);
    }

    [Test]
    public void EmptyPublicName_ShouldHaveError() {
        var request = new EventUpdateRequest {
            Id = Guid.NewGuid(),
            InternalName = "Test",
            PublicName = ""
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PublicName)
            .WithErrorMessage(I18nKey.Error.Event.PublicNameRequired);
    }

    [Test]
    public void PublicNameExceedsMaxLength_ShouldHaveError() {
        var request = new EventUpdateRequest {
            Id = Guid.NewGuid(),
            InternalName = "Test",
            PublicName = new string('A', 513)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PublicName)
            .WithErrorMessage(I18nKey.Error.Event.PublicNameMaxLength);
    }
}

public class EventFromTemplateRequestValidatorTests {
    private readonly EventFromTemplateRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new EventFromTemplateRequest {
            TemplateId = Guid.NewGuid(),
            ChapterId = Guid.NewGuid()
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyTemplateId_ShouldHaveError() {
        var request = new EventFromTemplateRequest {
            TemplateId = Guid.Empty,
            ChapterId = Guid.NewGuid()
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TemplateId)
            .WithErrorMessage(I18nKey.Error.Event.Template.TemplateRequired);
    }

    [Test]
    public void EmptyChapterId_ShouldHaveError() {
        var request = new EventFromTemplateRequest {
            TemplateId = Guid.NewGuid(),
            ChapterId = Guid.Empty
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorMessage(I18nKey.Error.Event.Template.ChapterRequired);
    }

    [Test]
    public void BothIdsEmpty_ShouldHaveErrorsForBoth() {
        var request = new EventFromTemplateRequest {
            TemplateId = Guid.Empty,
            ChapterId = Guid.Empty
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TemplateId);
        result.ShouldHaveValidationErrorFor(x => x.ChapterId);
    }
}

public class EventTemplateCreateRequestValidatorTests {
    private readonly EventTemplateCreateRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new EventTemplateCreateRequest {
            EventId = Guid.NewGuid(),
            Name = "My Template"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyEventId_ShouldHaveError() {
        var request = new EventTemplateCreateRequest {
            EventId = Guid.Empty,
            Name = "My Template"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EventId)
            .WithErrorMessage(I18nKey.Error.Event.Template.EventRequired);
    }

    [Test]
    public void EmptyName_ShouldHaveError() {
        var request = new EventTemplateCreateRequest {
            EventId = Guid.NewGuid(),
            Name = ""
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(I18nKey.Error.Event.Template.NameRequired);
    }

    [Test]
    public void NameExceedsMaxLength_ShouldHaveError() {
        var request = new EventTemplateCreateRequest {
            EventId = Guid.NewGuid(),
            Name = new string('A', 513)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(I18nKey.Error.Event.Template.NameMaxLength);
    }

    [Test]
    public void NameAtMaxLength_ShouldHaveNoError() {
        var request = new EventTemplateCreateRequest {
            EventId = Guid.NewGuid(),
            Name = new string('A', 512)
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}
