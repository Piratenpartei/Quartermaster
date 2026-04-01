# Input Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add FluentValidation validators for all state-changing (POST/PUT/DELETE) endpoints to enforce required fields, string length limits matching database columns, email format, and enum ranges.

**Architecture:** FastEndpoints' `Validator<T>` base class auto-integrates with the request pipeline — no manual wiring needed. All validators live in the Server project (which references FastEndpoints). `AddFastEndpoints()` in Program.cs discovers them automatically. A test project using xUnit validates each rule.

**Tech Stack:** FastEndpoints `Validator<T>`, FluentValidation rules, xUnit, FluentAssertions

---

## File Structure

### New files — Validators (Server project)

| File | Responsibility |
|---|---|
| `Quartermaster.Server/Events/EventCreateRequestValidator.cs` | Validates event creation (ChapterId, names ≤512) |
| `Quartermaster.Server/Events/EventUpdateRequestValidator.cs` | Validates event update (Id, names ≤512) |
| `Quartermaster.Server/Events/EventFromTemplateRequestValidator.cs` | Validates template instantiation (TemplateId, ChapterId) |
| `Quartermaster.Server/Events/EventTemplateCreateRequestValidator.cs` | Validates template creation (EventId, Name ≤512) |
| `Quartermaster.Server/Events/ChecklistItemCreateRequestValidator.cs` | Validates checklist add (EventId, Label ≤1024, ItemType 0–2) |
| `Quartermaster.Server/Events/ChecklistItemUpdateRequestValidator.cs` | Validates checklist edit (EventId, ItemId, Label ≤1024, ItemType 0–2) |
| `Quartermaster.Server/Events/ChecklistItemReorderRequestValidator.cs` | Validates reorder (EventId, ItemId, Direction ±1) |
| `Quartermaster.Server/Motions/MotionCreateRequestValidator.cs` | Validates motion creation (all fields required, email contains @, lengths) |
| `Quartermaster.Server/Motions/MotionStatusRequestValidator.cs` | Validates status change (MotionId) |
| `Quartermaster.Server/Motions/MotionVoteRequestValidator.cs` | Validates vote (MotionId, UserId, Vote 0–2) |
| `Quartermaster.Server/MembershipApplications/MembershipApplicationDTOValidator.cs` | Validates application (all personal fields, email, lengths) |
| `Quartermaster.Server/DueSelector/DueSelectionDTOValidator.cs` | Validates due selection (names, IBAN ≤64, AccountHolder ≤256) |
| `Quartermaster.Server/ChapterAssociates/ChapterOfficerAddRequestValidator.cs` | Validates officer add (MemberId, ChapterId, AssociateType 0–6) |
| `Quartermaster.Server/Admin/DueSelectionProcessRequestValidator.cs` | Validates process action (Id, Status 1–2) |
| `Quartermaster.Server/Admin/MembershipApplicationProcessRequestValidator.cs` | Validates process action (Id, Status 1–2) |
| `Quartermaster.Server/Options/OptionUpdateRequestValidator.cs` | Validates option save (Identifier required, Value ≤8192) |
| `Quartermaster.Server/Options/TemplatePreviewRequestValidator.cs` | Validates preview (TemplateText required) |
| `Quartermaster.Server/Users/LoginRequestValidator.cs` | Validates login (Username or Email required, Password ≥12) |

### New files — Test project

| File | Responsibility |
|---|---|
| `Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj` | xUnit test project |
| `Quartermaster.Server.Tests/Events/EventValidatorTests.cs` | Tests for all 7 event/checklist validators |
| `Quartermaster.Server.Tests/Motions/MotionValidatorTests.cs` | Tests for all 3 motion validators |
| `Quartermaster.Server.Tests/MembershipApplications/MembershipApplicationDTOValidatorTests.cs` | Tests for application validator |
| `Quartermaster.Server.Tests/DueSelector/DueSelectionDTOValidatorTests.cs` | Tests for due selection validator |
| `Quartermaster.Server.Tests/ChapterAssociates/ChapterOfficerAddRequestValidatorTests.cs` | Tests for officer validator |
| `Quartermaster.Server.Tests/Admin/AdminProcessValidatorTests.cs` | Tests for both process validators |
| `Quartermaster.Server.Tests/Options/OptionValidatorTests.cs` | Tests for option + template preview validators |
| `Quartermaster.Server.Tests/Users/LoginRequestValidatorTests.cs` | Tests for login validator |

### Modified files

| File | Change |
|---|---|
| `Quartermaster.sln` | Add test project |
| `Quartermaster.Api/Users/LoginRequest.cs` | Remove commented-out validator code |

---

## Validation Rules Reference

### Database column sizes (from M001_InitialStructureMigration.cs)

| Entity | Column | DB Size |
|---|---|---|
| Event | InternalName | 512 |
| Event | PublicName | 512 |
| EventChecklistItem | Label | 1024 |
| EventTemplate | Name | 512 |
| Motion | AuthorName | 256 |
| Motion | AuthorEMail | 256 |
| Motion | Title | 512 |
| Motion | Text | 8192 |
| MembershipApplication | FirstName | 256 |
| MembershipApplication | LastName | 256 |
| MembershipApplication | Citizenship | 256 |
| MembershipApplication | EMail | 256 |
| MembershipApplication | PhoneNumber | 64 |
| MembershipApplication | AddressStreet | 256 |
| MembershipApplication | AddressHouseNbr | 32 |
| MembershipApplication | AddressPostCode | 16 |
| MembershipApplication | AddressCity | 256 |
| MembershipApplication | ApplicationText | 2048 |
| DueSelection | ReducedJustification | 2048 |
| DueSelection | AccountHolder | 256 |
| DueSelection | IBAN | 64 |
| SystemOption | Value | 8192 |
| User | Username | 64 |
| User | EMail | 256 |
| Chapter | Name | 256 |

### Enum ranges

| Enum | Valid range |
|---|---|
| ChecklistItemType | 0 (Text), 1 (CreateMotion), 2 (SendEmail) |
| ChapterOfficerType | 0–6 (Captain through Member) |
| MotionApprovalStatus | 0–4 (Pending through ClosedWithoutAction) |
| VoteType | 0 (Approve), 1 (Deny), 2 (Abstain) |
| DueSelectionStatus (process) | 1 (Approved), 2 (Rejected) only |
| ApplicationStatus (process) | 1 (Approved), 2 (Rejected) only |

---

## Tasks

### Task 1: Create test project

**Files:**
- Create: `Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj`
- Modify: `Quartermaster.sln`

- [ ] **Step 1: Create the test project using dotnet CLI**

```bash
cd /media/SMB/Quartermaster
dotnet new xunit -n Quartermaster.Server.Tests --framework net10.0
dotnet sln add Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
dotnet add Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj reference Quartermaster.Server/Quartermaster.Server.csproj
dotnet add Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj reference Quartermaster.Api/Quartermaster.Api.csproj
dotnet add Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj package FluentAssertions
```

- [ ] **Step 2: Remove the auto-generated test file**

Delete `Quartermaster.Server.Tests/UnitTest1.cs` (or `Test1.cs`) — it's a template placeholder.

- [ ] **Step 3: Verify the project builds**

```bash
cd /media/SMB/Quartermaster
dotnet build Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj Quartermaster.sln
git commit -m "chore: add xUnit test project for server validators"
```

---

### Task 2: Event validators

**Files:**
- Create: `Quartermaster.Server/Events/EventCreateRequestValidator.cs`
- Create: `Quartermaster.Server/Events/EventUpdateRequestValidator.cs`
- Create: `Quartermaster.Server/Events/EventFromTemplateRequestValidator.cs`
- Create: `Quartermaster.Server/Events/EventTemplateCreateRequestValidator.cs`
- Test: `Quartermaster.Server.Tests/Events/EventValidatorTests.cs`

**Context:** Request DTOs are in `Quartermaster.Api/Events/`. Validators use `FastEndpoints.Validator<T>` base class. All validation messages are in German.

- [ ] **Step 1: Write the test file**

```csharp
using FluentAssertions;
using FluentValidation.TestHelper;
using Quartermaster.Api.Events;
using Quartermaster.Server.Events;

namespace Quartermaster.Server.Tests.Events;

public class EventCreateRequestValidatorTests {
    private readonly EventCreateRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = "Stammtisch",
            PublicName = "Stammtisch Hannover"
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyChapterId_Fails() {
        var model = new EventCreateRequest { ChapterId = Guid.Empty, InternalName = "X", PublicName = "X" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ChapterId);
    }

    [Fact]
    public void EmptyInternalName_Fails() {
        var model = new EventCreateRequest { ChapterId = Guid.NewGuid(), InternalName = "", PublicName = "X" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.InternalName);
    }

    [Fact]
    public void InternalNameTooLong_Fails() {
        var model = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = new string('A', 513),
            PublicName = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.InternalName);
    }

    [Fact]
    public void EmptyPublicName_Fails() {
        var model = new EventCreateRequest { ChapterId = Guid.NewGuid(), InternalName = "X", PublicName = "" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PublicName);
    }

    [Fact]
    public void PublicNameTooLong_Fails() {
        var model = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = "X",
            PublicName = new string('A', 513)
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PublicName);
    }
}

public class EventUpdateRequestValidatorTests {
    private readonly EventUpdateRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new EventUpdateRequest {
            Id = Guid.NewGuid(),
            InternalName = "Stammtisch",
            PublicName = "Stammtisch Hannover"
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyId_Fails() {
        var model = new EventUpdateRequest { Id = Guid.Empty, InternalName = "X", PublicName = "X" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void EmptyInternalName_Fails() {
        var model = new EventUpdateRequest { Id = Guid.NewGuid(), InternalName = "", PublicName = "X" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.InternalName);
    }

    [Fact]
    public void InternalNameTooLong_Fails() {
        var model = new EventUpdateRequest {
            Id = Guid.NewGuid(),
            InternalName = new string('A', 513),
            PublicName = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.InternalName);
    }

    [Fact]
    public void PublicNameTooLong_Fails() {
        var model = new EventUpdateRequest {
            Id = Guid.NewGuid(),
            InternalName = "X",
            PublicName = new string('A', 513)
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PublicName);
    }
}

public class EventFromTemplateRequestValidatorTests {
    private readonly EventFromTemplateRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new EventFromTemplateRequest {
            TemplateId = Guid.NewGuid(),
            ChapterId = Guid.NewGuid()
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyTemplateId_Fails() {
        var model = new EventFromTemplateRequest { TemplateId = Guid.Empty, ChapterId = Guid.NewGuid() };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TemplateId);
    }

    [Fact]
    public void EmptyChapterId_Fails() {
        var model = new EventFromTemplateRequest { TemplateId = Guid.NewGuid(), ChapterId = Guid.Empty };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ChapterId);
    }
}

public class EventTemplateCreateRequestValidatorTests {
    private readonly EventTemplateCreateRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new EventTemplateCreateRequest {
            EventId = Guid.NewGuid(),
            Name = "Stammtisch-Vorlage"
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEventId_Fails() {
        var model = new EventTemplateCreateRequest { EventId = Guid.Empty, Name = "X" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Fact]
    public void EmptyName_Fails() {
        var model = new EventTemplateCreateRequest { EventId = Guid.NewGuid(), Name = "" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameTooLong_Fails() {
        var model = new EventTemplateCreateRequest {
            EventId = Guid.NewGuid(),
            Name = new string('A', 513)
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~EventValidatorTests" --verbosity normal
```

Expected: Compilation errors — validator classes don't exist yet.

- [ ] **Step 3: Implement EventCreateRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class EventCreateRequestValidator : Validator<EventCreateRequest> {
    public EventCreateRequestValidator() {
        RuleFor(x => x.ChapterId)
            .NotEqual(Guid.Empty)
            .WithMessage("Gliederung muss ausgewählt werden.");
        RuleFor(x => x.InternalName)
            .NotEmpty().WithMessage("Interner Name darf nicht leer sein.")
            .MaximumLength(512).WithMessage("Interner Name darf maximal 512 Zeichen lang sein.");
        RuleFor(x => x.PublicName)
            .NotEmpty().WithMessage("Öffentlicher Name darf nicht leer sein.")
            .MaximumLength(512).WithMessage("Öffentlicher Name darf maximal 512 Zeichen lang sein.");
    }
}
```

- [ ] **Step 4: Implement EventUpdateRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class EventUpdateRequestValidator : Validator<EventUpdateRequest> {
    public EventUpdateRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("Event-ID darf nicht leer sein.");
        RuleFor(x => x.InternalName)
            .NotEmpty().WithMessage("Interner Name darf nicht leer sein.")
            .MaximumLength(512).WithMessage("Interner Name darf maximal 512 Zeichen lang sein.");
        RuleFor(x => x.PublicName)
            .NotEmpty().WithMessage("Öffentlicher Name darf nicht leer sein.")
            .MaximumLength(512).WithMessage("Öffentlicher Name darf maximal 512 Zeichen lang sein.");
    }
}
```

- [ ] **Step 5: Implement EventFromTemplateRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class EventFromTemplateRequestValidator : Validator<EventFromTemplateRequest> {
    public EventFromTemplateRequestValidator() {
        RuleFor(x => x.TemplateId)
            .NotEqual(Guid.Empty)
            .WithMessage("Vorlage muss ausgewählt werden.");
        RuleFor(x => x.ChapterId)
            .NotEqual(Guid.Empty)
            .WithMessage("Gliederung muss ausgewählt werden.");
    }
}
```

- [ ] **Step 6: Implement EventTemplateCreateRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateCreateRequestValidator : Validator<EventTemplateCreateRequest> {
    public EventTemplateCreateRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(Guid.Empty)
            .WithMessage("Event muss angegeben werden.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Vorlagenname darf nicht leer sein.")
            .MaximumLength(512).WithMessage("Vorlagenname darf maximal 512 Zeichen lang sein.");
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~EventValidatorTests" --verbosity normal
```

Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add Quartermaster.Server/Events/*Validator.cs Quartermaster.Server.Tests/Events/EventValidatorTests.cs
git commit -m "feat: add validators for event and template request DTOs"
```

---

### Task 3: Checklist item validators

**Files:**
- Create: `Quartermaster.Server/Events/ChecklistItemCreateRequestValidator.cs`
- Create: `Quartermaster.Server/Events/ChecklistItemUpdateRequestValidator.cs`
- Create: `Quartermaster.Server/Events/ChecklistItemReorderRequestValidator.cs`
- Test: `Quartermaster.Server.Tests/Events/ChecklistItemValidatorTests.cs`

**Context:** `ChecklistItemCreateRequest` and `ChecklistItemUpdateRequest` are in `Quartermaster.Api/Events/`. `ChecklistItemReorderRequest` is defined inside `Quartermaster.Server/Events/ChecklistItemReorderEndpoint.cs`. `ChecklistItemType` enum: Text=0, CreateMotion=1, SendEmail=2. Label max 1024 chars.

- [ ] **Step 1: Write the test file**

```csharp
using FluentAssertions;
using FluentValidation.TestHelper;
using Quartermaster.Api.Events;
using Quartermaster.Server.Events;

namespace Quartermaster.Server.Tests.Events;

public class ChecklistItemCreateRequestValidatorTests {
    private readonly ChecklistItemCreateRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            Label = "Send invitations",
            ItemType = 0,
            SortOrder = 1
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEventId_Fails() {
        var model = new ChecklistItemCreateRequest { EventId = Guid.Empty, Label = "X", ItemType = 0 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Fact]
    public void EmptyLabel_Fails() {
        var model = new ChecklistItemCreateRequest { EventId = Guid.NewGuid(), Label = "", ItemType = 0 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Label);
    }

    [Fact]
    public void LabelTooLong_Fails() {
        var model = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            Label = new string('A', 1025),
            ItemType = 0
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Label);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void InvalidItemType_Fails(int itemType) {
        var model = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            Label = "X",
            ItemType = itemType
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ItemType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ValidItemType_NoErrors(int itemType) {
        var model = new ChecklistItemCreateRequest {
            EventId = Guid.NewGuid(),
            Label = "X",
            ItemType = itemType
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.ItemType);
    }
}

public class ChecklistItemUpdateRequestValidatorTests {
    private readonly ChecklistItemUpdateRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Label = "Updated label",
            ItemType = 1,
            SortOrder = 2
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEventId_Fails() {
        var model = new ChecklistItemUpdateRequest {
            EventId = Guid.Empty, ItemId = Guid.NewGuid(), Label = "X", ItemType = 0
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Fact]
    public void EmptyItemId_Fails() {
        var model = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(), ItemId = Guid.Empty, Label = "X", ItemType = 0
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ItemId);
    }

    [Fact]
    public void EmptyLabel_Fails() {
        var model = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(), ItemId = Guid.NewGuid(), Label = "", ItemType = 0
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Label);
    }

    [Fact]
    public void LabelTooLong_Fails() {
        var model = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(), ItemId = Guid.NewGuid(),
            Label = new string('A', 1025), ItemType = 0
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Label);
    }

    [Fact]
    public void InvalidItemType_Fails() {
        var model = new ChecklistItemUpdateRequest {
            EventId = Guid.NewGuid(), ItemId = Guid.NewGuid(), Label = "X", ItemType = 5
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ItemType);
    }
}

public class ChecklistItemReorderRequestValidatorTests {
    private readonly ChecklistItemReorderRequestValidator _validator = new();

    [Fact]
    public void ValidMoveUp_NoErrors() {
        var model = new ChecklistItemReorderRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Direction = -1
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidMoveDown_NoErrors() {
        var model = new ChecklistItemReorderRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Direction = 1
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEventId_Fails() {
        var model = new ChecklistItemReorderRequest {
            EventId = Guid.Empty, ItemId = Guid.NewGuid(), Direction = 1
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Fact]
    public void EmptyItemId_Fails() {
        var model = new ChecklistItemReorderRequest {
            EventId = Guid.NewGuid(), ItemId = Guid.Empty, Direction = 1
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ItemId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-2)]
    public void InvalidDirection_Fails(int direction) {
        var model = new ChecklistItemReorderRequest {
            EventId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Direction = direction
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Direction);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~ChecklistItem" --verbosity normal
```

Expected: Compilation errors — validator classes don't exist yet.

- [ ] **Step 3: Implement ChecklistItemCreateRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemCreateRequestValidator : Validator<ChecklistItemCreateRequest> {
    public ChecklistItemCreateRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(Guid.Empty)
            .WithMessage("Event muss angegeben werden.");
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Bezeichnung darf nicht leer sein.")
            .MaximumLength(1024).WithMessage("Bezeichnung darf maximal 1024 Zeichen lang sein.");
        RuleFor(x => x.ItemType)
            .InclusiveBetween(0, 2)
            .WithMessage("Ungültiger Checklistentyp.");
    }
}
```

- [ ] **Step 4: Implement ChecklistItemUpdateRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemUpdateRequestValidator : Validator<ChecklistItemUpdateRequest> {
    public ChecklistItemUpdateRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(Guid.Empty)
            .WithMessage("Event muss angegeben werden.");
        RuleFor(x => x.ItemId)
            .NotEqual(Guid.Empty)
            .WithMessage("Element-ID darf nicht leer sein.");
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Bezeichnung darf nicht leer sein.")
            .MaximumLength(1024).WithMessage("Bezeichnung darf maximal 1024 Zeichen lang sein.");
        RuleFor(x => x.ItemType)
            .InclusiveBetween(0, 2)
            .WithMessage("Ungültiger Checklistentyp.");
    }
}
```

- [ ] **Step 5: Implement ChecklistItemReorderRequestValidator**

Note: `ChecklistItemReorderRequest` is defined inside `ChecklistItemReorderEndpoint.cs` in the `Quartermaster.Server.Events` namespace. The validator goes in its own file in the same namespace.

```csharp
using FastEndpoints;
using FluentValidation;

namespace Quartermaster.Server.Events;

public class ChecklistItemReorderRequestValidator : Validator<ChecklistItemReorderRequest> {
    public ChecklistItemReorderRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(Guid.Empty)
            .WithMessage("Event muss angegeben werden.");
        RuleFor(x => x.ItemId)
            .NotEqual(Guid.Empty)
            .WithMessage("Element-ID darf nicht leer sein.");
        RuleFor(x => x.Direction)
            .Must(d => d == -1 || d == 1)
            .WithMessage("Richtung muss -1 (hoch) oder 1 (runter) sein.");
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~ChecklistItem" --verbosity normal
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add Quartermaster.Server/Events/ChecklistItem*Validator.cs Quartermaster.Server.Tests/Events/ChecklistItemValidatorTests.cs
git commit -m "feat: add validators for checklist item request DTOs"
```

---

### Task 4: Motion validators

**Files:**
- Create: `Quartermaster.Server/Motions/MotionCreateRequestValidator.cs`
- Create: `Quartermaster.Server/Motions/MotionStatusRequestValidator.cs`
- Create: `Quartermaster.Server/Motions/MotionVoteRequestValidator.cs`
- Test: `Quartermaster.Server.Tests/Motions/MotionValidatorTests.cs`

**Context:** Request DTOs are in `Quartermaster.Api/Motions/`. Email validation: `Contains('@')` is sufficient. DB limits: AuthorName 256, AuthorEMail 256, Title 512, Text 8192. VoteType: 0–2. MotionApprovalStatus: 0–4.

- [ ] **Step 1: Write the test file**

```csharp
using FluentAssertions;
using FluentValidation.TestHelper;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Motions;

namespace Quartermaster.Server.Tests.Motions;

public class MotionCreateRequestValidatorTests {
    private readonly MotionCreateRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Max Mustermann",
            AuthorEMail = "max@example.com",
            Title = "Antrag auf Satzungsänderung",
            Text = "Hiermit beantrage ich..."
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyChapterId_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.Empty, AuthorName = "X", AuthorEMail = "x@y", Title = "X", Text = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ChapterId);
    }

    [Fact]
    public void EmptyAuthorName_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(), AuthorName = "", AuthorEMail = "x@y", Title = "X", Text = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AuthorName);
    }

    [Fact]
    public void AuthorNameTooLong_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = new string('A', 257),
            AuthorEMail = "x@y", Title = "X", Text = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AuthorName);
    }

    [Fact]
    public void EmptyEmail_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(), AuthorName = "X", AuthorEMail = "", Title = "X", Text = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AuthorEMail);
    }

    [Fact]
    public void EmailWithoutAt_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(), AuthorName = "X", AuthorEMail = "invalid", Title = "X", Text = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AuthorEMail);
    }

    [Fact]
    public void EmailTooLong_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(), AuthorName = "X",
            AuthorEMail = new string('a', 250) + "@x.com",
            Title = "X", Text = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AuthorEMail);
    }

    [Fact]
    public void EmptyTitle_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(), AuthorName = "X", AuthorEMail = "x@y", Title = "", Text = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void TitleTooLong_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(), AuthorName = "X", AuthorEMail = "x@y",
            Title = new string('A', 513), Text = "X"
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void EmptyText_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(), AuthorName = "X", AuthorEMail = "x@y", Title = "X", Text = ""
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void TextTooLong_Fails() {
        var model = new MotionCreateRequest {
            ChapterId = Guid.NewGuid(), AuthorName = "X", AuthorEMail = "x@y",
            Title = "X", Text = new string('A', 8193)
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }
}

public class MotionStatusRequestValidatorTests {
    private readonly MotionStatusRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new MotionStatusRequest { MotionId = Guid.NewGuid(), ApprovalStatus = 1 };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyMotionId_Fails() {
        var model = new MotionStatusRequest { MotionId = Guid.Empty };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.MotionId);
    }
}

public class MotionVoteRequestValidatorTests {
    private readonly MotionVoteRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new MotionVoteRequest {
            MotionId = Guid.NewGuid(), UserId = Guid.NewGuid(), Vote = 0
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyMotionId_Fails() {
        var model = new MotionVoteRequest { MotionId = Guid.Empty, UserId = Guid.NewGuid(), Vote = 0 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.MotionId);
    }

    [Fact]
    public void EmptyUserId_Fails() {
        var model = new MotionVoteRequest { MotionId = Guid.NewGuid(), UserId = Guid.Empty, Vote = 0 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void InvalidVote_Fails(int vote) {
        var model = new MotionVoteRequest {
            MotionId = Guid.NewGuid(), UserId = Guid.NewGuid(), Vote = vote
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Vote);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~MotionValidatorTests" --verbosity normal
```

Expected: Compilation errors.

- [ ] **Step 3: Implement MotionCreateRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionCreateRequestValidator : Validator<MotionCreateRequest> {
    public MotionCreateRequestValidator() {
        RuleFor(x => x.ChapterId)
            .NotEqual(Guid.Empty)
            .WithMessage("Gliederung muss ausgewählt werden.");
        RuleFor(x => x.AuthorName)
            .NotEmpty().WithMessage("Name des Antragstellers darf nicht leer sein.")
            .MaximumLength(256).WithMessage("Name des Antragstellers darf maximal 256 Zeichen lang sein.");
        RuleFor(x => x.AuthorEMail)
            .NotEmpty().WithMessage("E-Mail-Adresse darf nicht leer sein.")
            .Must(e => e != null && e.Contains('@')).WithMessage("E-Mail-Adresse muss ein @ enthalten.")
            .MaximumLength(256).WithMessage("E-Mail-Adresse darf maximal 256 Zeichen lang sein.");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Titel darf nicht leer sein.")
            .MaximumLength(512).WithMessage("Titel darf maximal 512 Zeichen lang sein.");
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Antragstext darf nicht leer sein.")
            .MaximumLength(8192).WithMessage("Antragstext darf maximal 8192 Zeichen lang sein.");
    }
}
```

- [ ] **Step 4: Implement MotionStatusRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionStatusRequestValidator : Validator<MotionStatusRequest> {
    public MotionStatusRequestValidator() {
        RuleFor(x => x.MotionId)
            .NotEqual(Guid.Empty)
            .WithMessage("Antrags-ID darf nicht leer sein.");
    }
}
```

- [ ] **Step 5: Implement MotionVoteRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionVoteRequestValidator : Validator<MotionVoteRequest> {
    public MotionVoteRequestValidator() {
        RuleFor(x => x.MotionId)
            .NotEqual(Guid.Empty)
            .WithMessage("Antrags-ID darf nicht leer sein.");
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Benutzer-ID darf nicht leer sein.");
        RuleFor(x => x.Vote)
            .InclusiveBetween(0, 2)
            .WithMessage("Ungültige Abstimmung.");
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~MotionValidatorTests" --verbosity normal
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add Quartermaster.Server/Motions/*Validator.cs Quartermaster.Server.Tests/Motions/MotionValidatorTests.cs
git commit -m "feat: add validators for motion request DTOs"
```

---

### Task 5: Public-facing validators (MembershipApplication + DueSelection)

**Files:**
- Create: `Quartermaster.Server/MembershipApplications/MembershipApplicationDTOValidator.cs`
- Create: `Quartermaster.Server/DueSelector/DueSelectionDTOValidator.cs`
- Test: `Quartermaster.Server.Tests/MembershipApplications/MembershipApplicationDTOValidatorTests.cs`
- Test: `Quartermaster.Server.Tests/DueSelector/DueSelectionDTOValidatorTests.cs`

**Context:** These are the public-facing forms — highest priority for validation. `MembershipApplicationDTO` is in `Quartermaster.Api/MembershipApplications/`, `DueSelectionDTO` is in `Quartermaster.Api/DueSelector/`. Email validation: `Contains('@')` is sufficient. DB limits listed in the reference table above.

- [ ] **Step 1: Write MembershipApplicationDTOValidatorTests**

```csharp
using FluentAssertions;
using FluentValidation.TestHelper;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.DueSelector;
using Quartermaster.Server.MembershipApplications;

namespace Quartermaster.Server.Tests.MembershipApplications;

public class MembershipApplicationDTOValidatorTests {
    private readonly MembershipApplicationDTOValidator _validator = new();

    private static MembershipApplicationDTO ValidApplication() => new() {
        FirstName = "Max",
        LastName = "Mustermann",
        DateOfBirth = new DateTime(1990, 1, 1),
        Citizenship = "Deutsch",
        EMail = "max@example.com",
        PhoneNumber = "0511-12345",
        AddressStreet = "Musterstraße",
        AddressHouseNbr = "42",
        AddressPostCode = "30159",
        AddressCity = "Hannover",
        ConformityDeclarationAccepted = true,
        ApplicationText = "Ich möchte Mitglied werden."
    };

    [Fact]
    public void ValidRequest_NoErrors() {
        var result = _validator.TestValidate(ValidApplication());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmptyFirstName_Fails(string? firstName) {
        var model = ValidApplication();
        model.FirstName = firstName!;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void FirstNameTooLong_Fails() {
        var model = ValidApplication();
        model.FirstName = new string('A', 257);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void EmptyLastName_Fails() {
        var model = ValidApplication();
        model.LastName = "";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void LastNameTooLong_Fails() {
        var model = ValidApplication();
        model.LastName = new string('A', 257);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void EmptyEmail_Fails() {
        var model = ValidApplication();
        model.EMail = "";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EMail);
    }

    [Fact]
    public void EmailWithoutAt_Fails() {
        var model = ValidApplication();
        model.EMail = "invalid-email";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EMail);
    }

    [Fact]
    public void EmailTooLong_Fails() {
        var model = ValidApplication();
        model.EMail = new string('a', 250) + "@x.com";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EMail);
    }

    [Fact]
    public void EmptyCitizenship_Fails() {
        var model = ValidApplication();
        model.Citizenship = "";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Citizenship);
    }

    [Fact]
    public void CitizenshipTooLong_Fails() {
        var model = ValidApplication();
        model.Citizenship = new string('A', 257);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Citizenship);
    }

    [Fact]
    public void PhoneNumberTooLong_Fails() {
        var model = ValidApplication();
        model.PhoneNumber = new string('1', 65);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public void EmptyAddressStreet_Fails() {
        var model = ValidApplication();
        model.AddressStreet = "";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AddressStreet);
    }

    [Fact]
    public void AddressStreetTooLong_Fails() {
        var model = ValidApplication();
        model.AddressStreet = new string('A', 257);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AddressStreet);
    }

    [Fact]
    public void EmptyAddressHouseNbr_Fails() {
        var model = ValidApplication();
        model.AddressHouseNbr = "";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AddressHouseNbr);
    }

    [Fact]
    public void AddressHouseNbrTooLong_Fails() {
        var model = ValidApplication();
        model.AddressHouseNbr = new string('A', 33);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AddressHouseNbr);
    }

    [Fact]
    public void EmptyAddressPostCode_Fails() {
        var model = ValidApplication();
        model.AddressPostCode = "";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AddressPostCode);
    }

    [Fact]
    public void AddressPostCodeTooLong_Fails() {
        var model = ValidApplication();
        model.AddressPostCode = new string('1', 17);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AddressPostCode);
    }

    [Fact]
    public void EmptyAddressCity_Fails() {
        var model = ValidApplication();
        model.AddressCity = "";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AddressCity);
    }

    [Fact]
    public void AddressCityTooLong_Fails() {
        var model = ValidApplication();
        model.AddressCity = new string('A', 257);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AddressCity);
    }

    [Fact]
    public void ApplicationTextTooLong_Fails() {
        var model = ValidApplication();
        model.ApplicationText = new string('A', 2049);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ApplicationText);
    }

    [Fact]
    public void ConformityDeclarationNotAccepted_Fails() {
        var model = ValidApplication();
        model.ConformityDeclarationAccepted = false;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ConformityDeclarationAccepted);
    }
}
```

- [ ] **Step 2: Write DueSelectionDTOValidatorTests**

```csharp
using FluentAssertions;
using FluentValidation.TestHelper;
using Quartermaster.Api.DueSelector;
using Quartermaster.Server.DueSelector;

namespace Quartermaster.Server.Tests.DueSelector;

public class DueSelectionDTOValidatorTests {
    private readonly DueSelectionDTOValidator _validator = new();

    private static DueSelectionDTO ValidDueSelection() => new() {
        FirstName = "Max",
        LastName = "Mustermann",
        EMail = "max@example.com",
        SelectedDue = 48m,
        AccountHolder = "Max Mustermann",
        IBAN = "DE89370400440532013000"
    };

    [Fact]
    public void ValidRequest_NoErrors() {
        var result = _validator.TestValidate(ValidDueSelection());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyFirstName_Fails() {
        var model = ValidDueSelection();
        model.FirstName = "";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void EmptyLastName_Fails() {
        var model = ValidDueSelection();
        model.LastName = "";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void EmailWithoutAt_Fails() {
        var model = ValidDueSelection();
        model.EMail = "invalid";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EMail);
    }

    [Fact]
    public void AccountHolderTooLong_Fails() {
        var model = ValidDueSelection();
        model.AccountHolder = new string('A', 257);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AccountHolder);
    }

    [Fact]
    public void IBANTooLong_Fails() {
        var model = ValidDueSelection();
        model.IBAN = new string('A', 65);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.IBAN);
    }

    [Fact]
    public void ReducedJustificationTooLong_Fails() {
        var model = ValidDueSelection();
        model.ReducedJustification = new string('A', 2049);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ReducedJustification);
    }

    [Fact]
    public void NegativeSelectedDue_Fails() {
        var model = ValidDueSelection();
        model.SelectedDue = -1;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.SelectedDue);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~MembershipApplication or FullyQualifiedName~DueSelection" --verbosity normal
```

Expected: Compilation errors.

- [ ] **Step 4: Implement MembershipApplicationDTOValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Server.MembershipApplications;

public class MembershipApplicationDTOValidator : Validator<MembershipApplicationDTO> {
    public MembershipApplicationDTOValidator() {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Vorname darf nicht leer sein.")
            .MaximumLength(256).WithMessage("Vorname darf maximal 256 Zeichen lang sein.");
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Nachname darf nicht leer sein.")
            .MaximumLength(256).WithMessage("Nachname darf maximal 256 Zeichen lang sein.");
        RuleFor(x => x.EMail)
            .NotEmpty().WithMessage("E-Mail-Adresse darf nicht leer sein.")
            .Must(e => e != null && e.Contains('@')).WithMessage("E-Mail-Adresse muss ein @ enthalten.")
            .MaximumLength(256).WithMessage("E-Mail-Adresse darf maximal 256 Zeichen lang sein.");
        RuleFor(x => x.Citizenship)
            .NotEmpty().WithMessage("Staatsangehörigkeit darf nicht leer sein.")
            .MaximumLength(256).WithMessage("Staatsangehörigkeit darf maximal 256 Zeichen lang sein.");
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(64).WithMessage("Telefonnummer darf maximal 64 Zeichen lang sein.");
        RuleFor(x => x.AddressStreet)
            .NotEmpty().WithMessage("Straße darf nicht leer sein.")
            .MaximumLength(256).WithMessage("Straße darf maximal 256 Zeichen lang sein.");
        RuleFor(x => x.AddressHouseNbr)
            .NotEmpty().WithMessage("Hausnummer darf nicht leer sein.")
            .MaximumLength(32).WithMessage("Hausnummer darf maximal 32 Zeichen lang sein.");
        RuleFor(x => x.AddressPostCode)
            .NotEmpty().WithMessage("Postleitzahl darf nicht leer sein.")
            .MaximumLength(16).WithMessage("Postleitzahl darf maximal 16 Zeichen lang sein.");
        RuleFor(x => x.AddressCity)
            .NotEmpty().WithMessage("Stadt darf nicht leer sein.")
            .MaximumLength(256).WithMessage("Stadt darf maximal 256 Zeichen lang sein.");
        RuleFor(x => x.ApplicationText)
            .MaximumLength(2048).WithMessage("Antragstext darf maximal 2048 Zeichen lang sein.");
        RuleFor(x => x.ConformityDeclarationAccepted)
            .Equal(true).WithMessage("Die Grundsatzerklärung muss akzeptiert werden.");
    }
}
```

- [ ] **Step 5: Implement DueSelectionDTOValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.DueSelector;

namespace Quartermaster.Server.DueSelector;

public class DueSelectionDTOValidator : Validator<DueSelectionDTO> {
    public DueSelectionDTOValidator() {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Vorname darf nicht leer sein.");
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Nachname darf nicht leer sein.");
        RuleFor(x => x.EMail)
            .Must(e => string.IsNullOrEmpty(e) || e.Contains('@'))
            .WithMessage("E-Mail-Adresse muss ein @ enthalten.");
        RuleFor(x => x.SelectedDue)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Beitrag darf nicht negativ sein.");
        RuleFor(x => x.AccountHolder)
            .MaximumLength(256).WithMessage("Kontoinhaber darf maximal 256 Zeichen lang sein.");
        RuleFor(x => x.IBAN)
            .MaximumLength(64).WithMessage("IBAN darf maximal 64 Zeichen lang sein.");
        RuleFor(x => x.ReducedJustification)
            .MaximumLength(2048).WithMessage("Begründung darf maximal 2048 Zeichen lang sein.");
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~MembershipApplication or FullyQualifiedName~DueSelection" --verbosity normal
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add Quartermaster.Server/MembershipApplications/MembershipApplicationDTOValidator.cs \
       Quartermaster.Server/DueSelector/DueSelectionDTOValidator.cs \
       Quartermaster.Server.Tests/MembershipApplications/MembershipApplicationDTOValidatorTests.cs \
       Quartermaster.Server.Tests/DueSelector/DueSelectionDTOValidatorTests.cs
git commit -m "feat: add validators for membership application and due selection DTOs"
```

---

### Task 6: Admin, auth, and misc validators

**Files:**
- Create: `Quartermaster.Server/ChapterAssociates/ChapterOfficerAddRequestValidator.cs`
- Create: `Quartermaster.Server/Admin/DueSelectionProcessRequestValidator.cs`
- Create: `Quartermaster.Server/Admin/MembershipApplicationProcessRequestValidator.cs`
- Create: `Quartermaster.Server/Options/OptionUpdateRequestValidator.cs`
- Create: `Quartermaster.Server/Options/TemplatePreviewRequestValidator.cs`
- Create: `Quartermaster.Server/Users/LoginRequestValidator.cs`
- Test: `Quartermaster.Server.Tests/ChapterAssociates/ChapterOfficerAddRequestValidatorTests.cs`
- Test: `Quartermaster.Server.Tests/Admin/AdminProcessValidatorTests.cs`
- Test: `Quartermaster.Server.Tests/Options/OptionValidatorTests.cs`
- Test: `Quartermaster.Server.Tests/Users/LoginRequestValidatorTests.cs`

**Context:**
- `ChapterOfficerAddRequest` is in `Quartermaster.Api/ChapterAssociates/`. ChapterOfficerType: 0–6.
- `DueSelectionProcessRequest` is defined in `Quartermaster.Server/Admin/DueSelectionProcessEndpoint.cs`. Valid Status: 1 (Approved) or 2 (Rejected).
- `MembershipApplicationProcessRequest` is defined in `Quartermaster.Server/Admin/MembershipApplicationProcessEndpoint.cs`. Valid Status: 1 or 2.
- `OptionUpdateRequest` is in `Quartermaster.Api/Options/`. Value max 8192.
- `TemplatePreviewRequest` is in `Quartermaster.Api/Options/`. TemplateText required.
- `LoginRequest` is in `Quartermaster.Api/Users/`. Username OR Email required, Password min 12 chars.

- [ ] **Step 1: Write ChapterOfficerAddRequestValidatorTests**

```csharp
using FluentValidation.TestHelper;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Server.ChapterAssociates;

namespace Quartermaster.Server.Tests.ChapterAssociates;

public class ChapterOfficerAddRequestValidatorTests {
    private readonly ChapterOfficerAddRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(),
            ChapterId = Guid.NewGuid(),
            AssociateType = 0
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyMemberId_Fails() {
        var model = new ChapterOfficerAddRequest {
            MemberId = Guid.Empty, ChapterId = Guid.NewGuid(), AssociateType = 0
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.MemberId);
    }

    [Fact]
    public void EmptyChapterId_Fails() {
        var model = new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(), ChapterId = Guid.Empty, AssociateType = 0
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ChapterId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void InvalidAssociateType_Fails(int type) {
        var model = new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(), ChapterId = Guid.NewGuid(), AssociateType = type
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AssociateType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void ValidAssociateType_NoErrors(int type) {
        var model = new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(), ChapterId = Guid.NewGuid(), AssociateType = type
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.AssociateType);
    }
}
```

- [ ] **Step 2: Write AdminProcessValidatorTests**

```csharp
using FluentValidation.TestHelper;
using Quartermaster.Server.Admin;

namespace Quartermaster.Server.Tests.Admin;

public class DueSelectionProcessRequestValidatorTests {
    private readonly DueSelectionProcessRequestValidator _validator = new();

    [Fact]
    public void ValidApproval_NoErrors() {
        var model = new DueSelectionProcessRequest { Id = Guid.NewGuid(), Status = 1 };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRejection_NoErrors() {
        var model = new DueSelectionProcessRequest { Id = Guid.NewGuid(), Status = 2 };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyId_Fails() {
        var model = new DueSelectionProcessRequest { Id = Guid.Empty, Status = 1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void InvalidStatus_Fails(int status) {
        var model = new DueSelectionProcessRequest { Id = Guid.NewGuid(), Status = status };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
}

public class MembershipApplicationProcessRequestValidatorTests {
    private readonly MembershipApplicationProcessRequestValidator _validator = new();

    [Fact]
    public void ValidApproval_NoErrors() {
        var model = new MembershipApplicationProcessRequest { Id = Guid.NewGuid(), Status = 1 };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRejection_NoErrors() {
        var model = new MembershipApplicationProcessRequest { Id = Guid.NewGuid(), Status = 2 };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyId_Fails() {
        var model = new MembershipApplicationProcessRequest { Id = Guid.Empty, Status = 1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void InvalidStatus_Fails(int status) {
        var model = new MembershipApplicationProcessRequest { Id = Guid.NewGuid(), Status = status };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
}
```

- [ ] **Step 3: Write OptionValidatorTests**

```csharp
using FluentValidation.TestHelper;
using Quartermaster.Api.Options;
using Quartermaster.Server.Options;

namespace Quartermaster.Server.Tests.Options;

public class OptionUpdateRequestValidatorTests {
    private readonly OptionUpdateRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new OptionUpdateRequest { Identifier = "some.setting", Value = "value" };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyIdentifier_Fails() {
        var model = new OptionUpdateRequest { Identifier = "", Value = "value" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Identifier);
    }

    [Fact]
    public void ValueTooLong_Fails() {
        var model = new OptionUpdateRequest { Identifier = "key", Value = new string('A', 8193) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Value);
    }
}

public class TemplatePreviewRequestValidatorTests {
    private readonly TemplatePreviewRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors() {
        var model = new TemplatePreviewRequest { TemplateText = "Hello {{name}}", TemplateModels = "model" };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyTemplateText_Fails() {
        var model = new TemplatePreviewRequest { TemplateText = "", TemplateModels = "" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TemplateText);
    }
}
```

- [ ] **Step 4: Write LoginRequestValidatorTests**

```csharp
using FluentValidation.TestHelper;
using Quartermaster.Api.Users;
using Quartermaster.Server.Users;

namespace Quartermaster.Server.Tests.Users;

public class LoginRequestValidatorTests {
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void ValidWithUsername_NoErrors() {
        var model = new LoginRequest { Username = "admin", Password = "123456789012" };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidWithEmail_NoErrors() {
        var model = new LoginRequest { EMail = "admin@example.com", Password = "123456789012" };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NeitherUsernameNorEmail_Fails() {
        var model = new LoginRequest { Password = "123456789012" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void PasswordTooShort_Fails() {
        var model = new LoginRequest { Username = "admin", Password = "short" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void PasswordExactly12Chars_NoErrors() {
        var model = new LoginRequest { Username = "admin", Password = "123456789012" };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}
```

- [ ] **Step 5: Run tests to verify they fail**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~ChapterOfficerAdd or FullyQualifiedName~Process or FullyQualifiedName~Option or FullyQualifiedName~Login" --verbosity normal
```

Expected: Compilation errors.

- [ ] **Step 6: Implement ChapterOfficerAddRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.ChapterAssociates;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerAddRequestValidator : Validator<ChapterOfficerAddRequest> {
    public ChapterOfficerAddRequestValidator() {
        RuleFor(x => x.MemberId)
            .NotEqual(Guid.Empty)
            .WithMessage("Mitglied muss ausgewählt werden.");
        RuleFor(x => x.ChapterId)
            .NotEqual(Guid.Empty)
            .WithMessage("Gliederung muss ausgewählt werden.");
        RuleFor(x => x.AssociateType)
            .InclusiveBetween(0, 6)
            .WithMessage("Ungültiger Vorstandstyp.");
    }
}
```

- [ ] **Step 7: Implement DueSelectionProcessRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;

namespace Quartermaster.Server.Admin;

public class DueSelectionProcessRequestValidator : Validator<DueSelectionProcessRequest> {
    public DueSelectionProcessRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("Beitragsauswahl-ID darf nicht leer sein.");
        RuleFor(x => x.Status)
            .InclusiveBetween(1, 2)
            .WithMessage("Status muss 'Genehmigt' oder 'Abgelehnt' sein.");
    }
}
```

- [ ] **Step 8: Implement MembershipApplicationProcessRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationProcessRequestValidator : Validator<MembershipApplicationProcessRequest> {
    public MembershipApplicationProcessRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("Antrags-ID darf nicht leer sein.");
        RuleFor(x => x.Status)
            .InclusiveBetween(1, 2)
            .WithMessage("Status muss 'Genehmigt' oder 'Abgelehnt' sein.");
    }
}
```

- [ ] **Step 9: Implement OptionUpdateRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Options;

namespace Quartermaster.Server.Options;

public class OptionUpdateRequestValidator : Validator<OptionUpdateRequest> {
    public OptionUpdateRequestValidator() {
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .WithMessage("Bezeichner darf nicht leer sein.");
        RuleFor(x => x.Value)
            .MaximumLength(8192)
            .WithMessage("Wert darf maximal 8192 Zeichen lang sein.");
    }
}
```

- [ ] **Step 10: Implement TemplatePreviewRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Options;

namespace Quartermaster.Server.Options;

public class TemplatePreviewRequestValidator : Validator<TemplatePreviewRequest> {
    public TemplatePreviewRequestValidator() {
        RuleFor(x => x.TemplateText)
            .NotEmpty()
            .WithMessage("Vorlagentext darf nicht leer sein.");
    }
}
```

- [ ] **Step 11: Implement LoginRequestValidator**

```csharp
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Users;

namespace Quartermaster.Server.Users;

public class LoginRequestValidator : Validator<LoginRequest> {
    public LoginRequestValidator() {
        RuleFor(x => x.Username)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.EMail))
            .WithMessage("Benutzername oder E-Mail muss angegeben werden.");
        RuleFor(x => x.EMail)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.Username))
            .WithMessage("Benutzername oder E-Mail muss angegeben werden.");
        RuleFor(x => x.Password)
            .MinimumLength(12)
            .WithMessage("Das Passwort muss mindestens 12 Zeichen lang sein.");
    }
}
```

- [ ] **Step 12: Run tests to verify they pass**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --filter "FullyQualifiedName~ChapterOfficerAdd or FullyQualifiedName~Process or FullyQualifiedName~Option or FullyQualifiedName~Login" --verbosity normal
```

Expected: All tests pass.

- [ ] **Step 13: Commit**

```bash
git add Quartermaster.Server/ChapterAssociates/ChapterOfficerAddRequestValidator.cs \
       Quartermaster.Server/Admin/DueSelectionProcessRequestValidator.cs \
       Quartermaster.Server/Admin/MembershipApplicationProcessRequestValidator.cs \
       Quartermaster.Server/Options/OptionUpdateRequestValidator.cs \
       Quartermaster.Server/Options/TemplatePreviewRequestValidator.cs \
       Quartermaster.Server/Users/LoginRequestValidator.cs \
       Quartermaster.Server.Tests/ChapterAssociates/ \
       Quartermaster.Server.Tests/Admin/ \
       Quartermaster.Server.Tests/Options/ \
       Quartermaster.Server.Tests/Users/
git commit -m "feat: add validators for admin, auth, and option request DTOs"
```

---

### Task 7: Clean up and verify

**Files:**
- Modify: `Quartermaster.Api/Users/LoginRequest.cs` (remove commented-out validator)

- [ ] **Step 1: Remove commented-out validator from LoginRequest.cs**

In `Quartermaster.Api/Users/LoginRequest.cs`, remove all commented-out code. The file should only contain:

```csharp
namespace Quartermaster.Api.Users;

public class LoginRequest {
    public string? Username { get; set; }
    public string? EMail { get; set; }
    public required string Password { get; set; }
}
```

- [ ] **Step 2: Run full test suite**

```bash
cd /media/SMB/Quartermaster
dotnet test Quartermaster.Server.Tests --verbosity normal
```

Expected: All tests pass (should be ~60+ tests).

- [ ] **Step 3: Build and start the server to verify no runtime issues**

```bash
cd /media/SMB/Quartermaster
dotnet build Quartermaster.Server/Quartermaster.Server.csproj
dotnet run --project Quartermaster.Server/Quartermaster.Server.csproj
```

Verify: Server starts without errors. FastEndpoints discovers all validators during startup.

- [ ] **Step 4: Commit cleanup**

```bash
git add Quartermaster.Api/Users/LoginRequest.cs
git commit -m "chore: remove commented-out validator from LoginRequest"
```

---

## Checklist: Production Readiness TODOs Covered

| TODO | Status |
|---|---|
| Add FluentValidation validators for all request DTOs | ✅ 18 validators |
| Validate page size limits (prevent requesting 100k records) | Not in scope (search endpoints — generic cap) |
| Email validation: Contains('@') is sufficient | ✅ MotionCreate, MembershipApplication, DueSelection |
| Validate string lengths match database column sizes | ✅ All string fields validated |
| Validate required fields (ChapterId, names, etc.) | ✅ All required fields validated |
