using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.Events;
using Quartermaster.Server.Events;

namespace Quartermaster.Server.Tests.Events;

public class ChecklistItemCreateRequestValidatorTests {
    private readonly ChecklistItemCreateRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            SortOrder = 1,
            ItemType = 0,
            Label = "Test Label"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyEventId_ShouldHaveError() {
        var request = new ChecklistItemCreateRequest {
            EventId = Guid.Empty,
            Label = "Test",
            ItemType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EventId)
            .WithErrorMessage("Event muss angegeben werden.");
    }

    [Test]
    public void EmptyLabel_ShouldHaveError() {
        var request = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            Label = "",
            ItemType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Label)
            .WithErrorMessage("Bezeichnung darf nicht leer sein.");
    }

    [Test]
    public void LabelExceedsMaxLength_ShouldHaveError() {
        var request = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            Label = new string('A', 1025),
            ItemType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Label)
            .WithErrorMessage("Bezeichnung darf maximal 1024 Zeichen lang sein.");
    }

    [Test]
    public void LabelAtMaxLength_ShouldHaveNoError() {
        var request = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            Label = new string('A', 1024),
            ItemType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Label);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public void ValidItemType_ShouldHaveNoError(int itemType) {
        var request = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            Label = "Test",
            ItemType = itemType
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.ItemType);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(3)]
    [Arguments(99)]
    public void InvalidItemType_ShouldHaveError(int itemType) {
        var request = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            Label = "Test",
            ItemType = itemType
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ItemType)
            .WithErrorMessage("Ungültiger Checklistentyp.");
    }
}

public class ChecklistItemUpdateRequestValidatorTests {
    private readonly ChecklistItemUpdateRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            SortOrder = 1,
            ItemType = 0,
            Label = "Test Label"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyEventId_ShouldHaveError() {
        var request = new ChecklistItemUpdateRequest {
            EventId = Guid.Empty,
            ItemId = Guid.NewGuid(),
            Label = "Test",
            ItemType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EventId)
            .WithErrorMessage("Event muss angegeben werden.");
    }

    [Test]
    public void EmptyItemId_ShouldHaveError() {
        var request = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.Empty,
            Label = "Test",
            ItemType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ItemId)
            .WithErrorMessage("Element-ID darf nicht leer sein.");
    }

    [Test]
    public void EmptyLabel_ShouldHaveError() {
        var request = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Label = "",
            ItemType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Label)
            .WithErrorMessage("Bezeichnung darf nicht leer sein.");
    }

    [Test]
    public void LabelExceedsMaxLength_ShouldHaveError() {
        var request = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Label = new string('A', 1025),
            ItemType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Label)
            .WithErrorMessage("Bezeichnung darf maximal 1024 Zeichen lang sein.");
    }

    [Test]
    public void LabelAtMaxLength_ShouldHaveNoError() {
        var request = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Label = new string('A', 1024),
            ItemType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Label);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public void ValidItemType_ShouldHaveNoError(int itemType) {
        var request = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Label = "Test",
            ItemType = itemType
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.ItemType);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(3)]
    [Arguments(99)]
    public void InvalidItemType_ShouldHaveError(int itemType) {
        var request = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Label = "Test",
            ItemType = itemType
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ItemType)
            .WithErrorMessage("Ungültiger Checklistentyp.");
    }
}

public class ChecklistItemReorderRequestValidatorTests {
    private readonly ChecklistItemReorderRequestValidator _validator = new();

    [Test]
    public void ValidRequest_Up_ShouldHaveNoErrors() {
        var request = new ChecklistItemReorderRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Direction = -1
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequest_Down_ShouldHaveNoErrors() {
        var request = new ChecklistItemReorderRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Direction = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyEventId_ShouldHaveError() {
        var request = new ChecklistItemReorderRequest {
            EventId = Guid.Empty,
            ItemId = Guid.NewGuid(),
            Direction = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EventId)
            .WithErrorMessage("Event muss angegeben werden.");
    }

    [Test]
    public void EmptyItemId_ShouldHaveError() {
        var request = new ChecklistItemReorderRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.Empty,
            Direction = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ItemId)
            .WithErrorMessage("Element-ID darf nicht leer sein.");
    }

    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public void ValidDirection_ShouldHaveNoError(int direction) {
        var request = new ChecklistItemReorderRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Direction = direction
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Direction);
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    [Arguments(-2)]
    [Arguments(99)]
    public void InvalidDirection_ShouldHaveError(int direction) {
        var request = new ChecklistItemReorderRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Direction = direction
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Direction)
            .WithErrorMessage("Richtung muss -1 (hoch) oder 1 (runter) sein.");
    }
}
