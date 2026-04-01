using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.Events;
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
            .WithErrorMessage("Gliederung muss ausgewählt werden.");
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
            .WithErrorMessage("Interner Name darf nicht leer sein.");
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
            .WithErrorMessage("Interner Name darf maximal 512 Zeichen lang sein.");
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
            .WithErrorMessage("Öffentlicher Name darf nicht leer sein.");
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
            .WithErrorMessage("Öffentlicher Name darf maximal 512 Zeichen lang sein.");
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
            .WithErrorMessage("Event-ID darf nicht leer sein.");
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
            .WithErrorMessage("Interner Name darf nicht leer sein.");
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
            .WithErrorMessage("Interner Name darf maximal 512 Zeichen lang sein.");
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
            .WithErrorMessage("Öffentlicher Name darf nicht leer sein.");
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
            .WithErrorMessage("Öffentlicher Name darf maximal 512 Zeichen lang sein.");
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
            .WithErrorMessage("Vorlage muss ausgewählt werden.");
    }

    [Test]
    public void EmptyChapterId_ShouldHaveError() {
        var request = new EventFromTemplateRequest {
            TemplateId = Guid.NewGuid(),
            ChapterId = Guid.Empty
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorMessage("Gliederung muss ausgewählt werden.");
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
            .WithErrorMessage("Event muss angegeben werden.");
    }

    [Test]
    public void EmptyName_ShouldHaveError() {
        var request = new EventTemplateCreateRequest {
            EventId = Guid.NewGuid(),
            Name = ""
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Vorlagenname darf nicht leer sein.");
    }

    [Test]
    public void NameExceedsMaxLength_ShouldHaveError() {
        var request = new EventTemplateCreateRequest {
            EventId = Guid.NewGuid(),
            Name = new string('A', 513)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Vorlagenname darf maximal 512 Zeichen lang sein.");
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
