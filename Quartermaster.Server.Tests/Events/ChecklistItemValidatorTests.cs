using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.EventRequired);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.LabelRequired);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.LabelMaxLength);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.TypeInvalid);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.EventRequired);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.ItemIdRequired);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.LabelRequired);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.LabelMaxLength);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.TypeInvalid);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.EventRequired);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.ItemIdRequired);
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
            .WithErrorMessage(I18nKey.Error.Event.Checklist.ReorderDirectionInvalid);
    }
}
