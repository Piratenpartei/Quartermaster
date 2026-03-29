# Membership Application Flow — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the membership application form (currently at members.piratenpartei.de) into Quartermaster as a multi-step wizard, integrating the existing dues selector and using administrative divisions for chapter auto-assignment.

**Architecture:** The wizard is split into 6 steps: personal data, address & chapter assignment, dues type selection (reusing existing pages), dues details + payment, declarations, and summary. The existing DueSelector flow remains fully standalone; the membership flow embeds a `DueSelectorEntryState` within its own `MembershipApplicationEntryState`. Chapter lookup walks up the admin division hierarchy to find the most specific chapter. All data model changes go into M001 since the project isn't released yet.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB, Blazor WASM, Riok.Mapperly, Bootstrap 5

---

## File Structure

### Data Model Changes (modify existing)
- `Quartermaster.Data/Chapters/Chapter.cs` — add `AdministrativeDivisionId`, `ParentChapterId`
- `Quartermaster.Data/Chapters/ChapterRepository.cs` — **create**, chapter CRUD + lookup by division
- `Quartermaster.Data/MembershipApplications/MembershipApplication.cs` — expand with all application fields
- `Quartermaster.Data/MembershipApplications/MembershipApplicationRepository.cs` — **create**
- `Quartermaster.Data/MembershipApplications/MembershipApplicationMapper.cs` — **create**
- `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs` — update Chapters + MembershipApplications tables
- `Quartermaster.Data/DbContext.cs` — register new tables/repositories
- `Quartermaster.Data/AdministrativeDivisions/AdministrativeDivisionRepository.cs` — add `GetAncestors` method

### API DTOs (new)
- `Quartermaster.Api/Chapters/ChapterDTO.cs` — chapter with division and parent
- `Quartermaster.Api/MembershipApplications/MembershipApplicationDTO.cs` — full application DTO

### Server Endpoints (new)
- `Quartermaster.Server/Chapters/ChapterForDivisionEndpoint.cs` — find chapter for a given division
- `Quartermaster.Server/Chapters/ChapterListEndpoint.cs` — list all chapters
- `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs` — submit application

### Blazor Pages (new)
- `Quartermaster.Blazor/Pages/MembershipApplication/MembershipApplicationEntryState.cs` — wizard state (embeds `DueSelectorEntryState`)
- `Quartermaster.Blazor/Pages/MembershipApplication/PersonalData.razor` + `.cs` — step 1
- `Quartermaster.Blazor/Pages/MembershipApplication/AddressAndChapter.razor` + `.cs` — step 2
- `Quartermaster.Blazor/Pages/MembershipApplication/DuesTypeSelection.razor` + `.cs` — step 3 (wraps existing dues pages)
- `Quartermaster.Blazor/Pages/MembershipApplication/Declarations.razor` + `.cs` — step 4
- `Quartermaster.Blazor/Pages/MembershipApplication/ApplicationSummary.razor` + `.cs` — step 5
- `Quartermaster.Blazor/Services/AppStateService.cs` — register new entry state

### Navigation
- `Quartermaster.Blazor/Layout/MainLayout.razor` — add "Mitgliedsantrag" nav link

---

## Task 1: Expand Chapter Data Model

**Files:**
- Modify: `Quartermaster.Data/Chapters/Chapter.cs`
- Modify: `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`

- [ ] **Step 1: Add fields to Chapter entity**

In `Quartermaster.Data/Chapters/Chapter.cs`, add `AdministrativeDivisionId` and `ParentChapterId`:

```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Chapters;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Chapter {
    public const string TableName = "Chapters";

    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid? AdministrativeDivisionId { get; set; }
    public Guid? ParentChapterId { get; set; }
}
```

- [ ] **Step 2: Update M001 migration — Chapters table**

In `M001_InitialStructureMigration.cs`, update the Chapters table creation to add the two new columns and their foreign keys. Replace the Chapters table block:

```csharp
Create.Table(Chapter.TableName)
    .WithColumn(nameof(Chapter.Id)).AsGuid().PrimaryKey().Indexed()
    .WithColumn(nameof(Chapter.Name)).AsString(256)
    .WithColumn(nameof(Chapter.AdministrativeDivisionId)).AsGuid().Nullable()
    .WithColumn(nameof(Chapter.ParentChapterId)).AsGuid().Nullable();
```

Add these foreign keys after the existing `FK_Users_ChapterId_Chapters_Id`:

```csharp
Create.ForeignKey("FK_Chapters_AdministrativeDivisionId_AdministrativeDivisions_Id")
    .FromTable(Chapter.TableName).ForeignColumn(nameof(Chapter.AdministrativeDivisionId))
    .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

Create.ForeignKey("FK_Chapters_ParentChapterId_Chapters_Id")
    .FromTable(Chapter.TableName).ForeignColumn(nameof(Chapter.ParentChapterId))
    .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));
```

Add matching deletes in `Down()`:

```csharp
Delete.ForeignKey("FK_Chapters_AdministrativeDivisionId_AdministrativeDivisions_Id")
    .OnTable(Chapter.TableName);
Delete.ForeignKey("FK_Chapters_ParentChapterId_Chapters_Id")
    .OnTable(Chapter.TableName);
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build Quartermaster.Data`
Expected: Build succeeded, 0 errors

---

## Task 2: Expand MembershipApplication Data Model

**Files:**
- Modify: `Quartermaster.Data/MembershipApplications/MembershipApplication.cs`
- Modify: `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`

- [ ] **Step 1: Expand MembershipApplication entity**

Replace the full content of `MembershipApplication.cs`:

```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.MembershipApplications;

[Table(TableName, IsColumnAttributeRequired = false)]
public class MembershipApplication {
    public const string TableName = "MembershipApplications";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    // Personal data
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public string Citizenship { get; set; } = "";
    public string EMail { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    // Address
    public string AddressStreet { get; set; } = "";
    public string AddressHouseNbr { get; set; } = "";
    public string AddressPostCode { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public Guid? AddressAdministrativeDivisionId { get; set; }

    // Chapter
    public Guid? ChapterId { get; set; }

    // Dues (references the DueSelection created alongside)
    public Guid? DueSelectionId { get; set; }

    // Declarations
    public bool ConformityDeclarationAccepted { get; set; }
    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }
    public string ApplicationText { get; set; } = "";

    // Entry date
    public DateTime EntryDate { get; set; }
    public DateTime SubmittedAt { get; set; }
}
```

- [ ] **Step 2: Update M001 migration — MembershipApplications table**

Replace the MembershipApplications table creation block in `M001_InitialStructureMigration.cs`. Remove the old `FK_MembershipApplications_UserId_Users_Id` foreign key since we no longer have `UserId`:

```csharp
Create.Table(MembershipApplication.TableName)
    .WithColumn(nameof(MembershipApplication.Id)).AsGuid().PrimaryKey().Indexed()
    .WithColumn(nameof(MembershipApplication.FirstName)).AsString(256)
    .WithColumn(nameof(MembershipApplication.LastName)).AsString(256)
    .WithColumn(nameof(MembershipApplication.DateOfBirth)).AsDateTime()
    .WithColumn(nameof(MembershipApplication.Citizenship)).AsString(256)
    .WithColumn(nameof(MembershipApplication.EMail)).AsString(256)
    .WithColumn(nameof(MembershipApplication.PhoneNumber)).AsString(64)
    .WithColumn(nameof(MembershipApplication.AddressStreet)).AsString(256)
    .WithColumn(nameof(MembershipApplication.AddressHouseNbr)).AsString(32)
    .WithColumn(nameof(MembershipApplication.AddressPostCode)).AsString(16)
    .WithColumn(nameof(MembershipApplication.AddressCity)).AsString(256)
    .WithColumn(nameof(MembershipApplication.AddressAdministrativeDivisionId)).AsGuid().Nullable()
    .WithColumn(nameof(MembershipApplication.ChapterId)).AsGuid().Nullable()
    .WithColumn(nameof(MembershipApplication.DueSelectionId)).AsGuid().Nullable()
    .WithColumn(nameof(MembershipApplication.ConformityDeclarationAccepted)).AsBoolean()
    .WithColumn(nameof(MembershipApplication.HasPriorDeclinedApplication)).AsBoolean()
    .WithColumn(nameof(MembershipApplication.IsMemberOfAnotherParty)).AsBoolean()
    .WithColumn(nameof(MembershipApplication.ApplicationText)).AsString(2048)
    .WithColumn(nameof(MembershipApplication.EntryDate)).AsDateTime()
    .WithColumn(nameof(MembershipApplication.SubmittedAt)).AsDateTime();

Create.ForeignKey("FK_MembershipApplications_AddressAdminDivId_AdministrativeDivisions_Id")
    .FromTable(MembershipApplication.TableName).ForeignColumn(nameof(MembershipApplication.AddressAdministrativeDivisionId))
    .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

Create.ForeignKey("FK_MembershipApplications_ChapterId_Chapters_Id")
    .FromTable(MembershipApplication.TableName).ForeignColumn(nameof(MembershipApplication.ChapterId))
    .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

Create.ForeignKey("FK_MembershipApplications_DueSelectionId_DueSelections_Id")
    .FromTable(MembershipApplication.TableName).ForeignColumn(nameof(MembershipApplication.DueSelectionId))
    .ToTable(DueSelection.TableName).PrimaryColumn(nameof(DueSelection.Id));
```

Update `Down()` — replace the old `FK_MembershipApplications_UserId_Users_Id` delete with:

```csharp
Delete.ForeignKey("FK_MembershipApplications_AddressAdminDivId_AdministrativeDivisions_Id")
    .OnTable(MembershipApplication.TableName);
Delete.ForeignKey("FK_MembershipApplications_ChapterId_Chapters_Id")
    .OnTable(MembershipApplication.TableName);
Delete.ForeignKey("FK_MembershipApplications_DueSelectionId_DueSelections_Id")
    .OnTable(MembershipApplication.TableName);
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build Quartermaster.Data`
Expected: Build succeeded, 0 errors

---

## Task 3: Chapter Repository & Chapter Lookup by Division

**Files:**
- Create: `Quartermaster.Data/Chapters/ChapterRepository.cs`
- Modify: `Quartermaster.Data/AdministrativeDivisions/AdministrativeDivisionRepository.cs`
- Modify: `Quartermaster.Data/DbContext.cs`

- [ ] **Step 1: Add GetAncestors to AdministrativeDivisionRepository**

Add this method to `AdministrativeDivisionRepository.cs`:

```csharp
public List<Guid> GetAncestorIds(Guid divisionId) {
    var ids = new List<Guid>();
    var current = Get(divisionId);
    while (current != null) {
        ids.Add(current.Id);
        if (current.ParentId == null || current.ParentId == current.Id)
            break;
        current = Get(current.ParentId.Value);
    }
    return ids;
}
```

- [ ] **Step 2: Create ChapterRepository**

Create `Quartermaster.Data/Chapters/ChapterRepository.cs`:

```csharp
using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Chapters;

public class ChapterRepository {
    private readonly DbContext _context;

    public ChapterRepository(DbContext context) {
        _context = context;
    }

    public Chapter? Get(Guid id)
        => _context.Chapters.Where(c => c.Id == id).FirstOrDefault();

    public List<Chapter> GetAll()
        => _context.Chapters.OrderBy(c => c.Name).ToList();

    public void Create(Chapter chapter) => _context.Insert(chapter);

    /// <summary>
    /// Finds the most specific chapter for a given admin division
    /// by walking up the hierarchy and checking each level.
    /// </summary>
    public Chapter? FindForDivision(Guid divisionId, AdministrativeDivisions.AdministrativeDivisionRepository adminDivRepo) {
        var ancestorIds = adminDivRepo.GetAncestorIds(divisionId);
        if (ancestorIds.Count == 0)
            return null;

        var chapters = _context.Chapters
            .Where(c => c.AdministrativeDivisionId != null && ancestorIds.Contains(c.AdministrativeDivisionId.Value))
            .ToList();

        if (chapters.Count == 0)
            return null;

        // Return the chapter whose division appears earliest in the ancestor list (most specific)
        foreach (var ancestorId in ancestorIds) {
            var match = chapters.FirstOrDefault(c => c.AdministrativeDivisionId == ancestorId);
            if (match != null)
                return match;
        }

        return chapters[0];
    }

    public void SupplementDefaults(AdministrativeDivisions.AdministrativeDivisionRepository adminDivRepo) {
        if (_context.Chapters.Any())
            return;

        // Find DE country division
        var deDivision = _context.GetTable<AdministrativeDivisions.AdministrativeDivision>()
            .Where(ad => ad.AdminCode == "DE" && ad.Depth == 3)
            .FirstOrDefault();
        if (deDivision == null)
            return;

        // Create Bundesverband (federal chapter)
        var bundesverband = new Chapter {
            Id = Guid.NewGuid(),
            Name = "Piratenpartei Deutschland",
            AdministrativeDivisionId = deDivision.Id,
            ParentChapterId = null
        };
        Create(bundesverband);

        // Create state chapters for each Bundesland (depth 4)
        var states = adminDivRepo.GetChildren(deDivision.Id);
        foreach (var state in states) {
            Create(new Chapter {
                Id = Guid.NewGuid(),
                Name = $"Piratenpartei {state.Name}",
                AdministrativeDivisionId = state.Id,
                ParentChapterId = bundesverband.Id
            });
        }
    }
}
```

- [ ] **Step 3: Register in DbContext**

In `DbContext.cs`, add `ITable<Chapter>` and register the repository:

Add to properties:
```csharp
public ITable<Chapter> Chapters => this.GetTable<Chapter>();
```

Add to `AddRepositories`:
```csharp
services.AddScoped<ChapterRepository>();
```

Add to `SupplementDefaults` (after AdminDivisions, before Permissions):
```csharp
scope.ServiceProvider.GetRequiredService<ChapterRepository>().SupplementDefaults(
    scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>());
```

Add the using:
```csharp
using Quartermaster.Data.Chapters;
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 4: MembershipApplication Repository

**Files:**
- Create: `Quartermaster.Data/MembershipApplications/MembershipApplicationRepository.cs`
- Modify: `Quartermaster.Data/DbContext.cs`

- [ ] **Step 1: Create MembershipApplicationRepository**

Create `Quartermaster.Data/MembershipApplications/MembershipApplicationRepository.cs`:

```csharp
using LinqToDB;
using System;

namespace Quartermaster.Data.MembershipApplications;

public class MembershipApplicationRepository {
    private readonly DbContext _context;

    public MembershipApplicationRepository(DbContext context) {
        _context = context;
    }

    public void Create(MembershipApplication application) => _context.Insert(application);
}
```

- [ ] **Step 2: Register in DbContext**

Add to `DbContext.cs`:

Property:
```csharp
public ITable<MembershipApplication> MembershipApplications => this.GetTable<MembershipApplication>();
```

In `AddRepositories`:
```csharp
services.AddScoped<MembershipApplicationRepository>();
```

Add the using:
```csharp
using Quartermaster.Data.MembershipApplications;
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 5: API DTOs

**Files:**
- Create: `Quartermaster.Api/Chapters/ChapterDTO.cs`
- Create: `Quartermaster.Api/MembershipApplications/MembershipApplicationDTO.cs`

- [ ] **Step 1: Create ChapterDTO**

Create `Quartermaster.Api/Chapters/ChapterDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.Chapters;

public class ChapterDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid? AdministrativeDivisionId { get; set; }
    public Guid? ParentChapterId { get; set; }
}
```

- [ ] **Step 2: Create MembershipApplicationDTO**

Create `Quartermaster.Api/MembershipApplications/MembershipApplicationDTO.cs`:

```csharp
using System;
using Quartermaster.Api.DueSelector;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationDTO {
    // Personal data
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public string Citizenship { get; set; } = "";
    public string EMail { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    // Address
    public string AddressStreet { get; set; } = "";
    public string AddressHouseNbr { get; set; } = "";
    public string AddressPostCode { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public Guid? AddressAdministrativeDivisionId { get; set; }

    // Chapter
    public Guid? ChapterId { get; set; }

    // Dues (embedded, submitted alongside)
    public DueSelectionDTO? DueSelection { get; set; }

    // Declarations
    public bool ConformityDeclarationAccepted { get; set; }
    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }
    public string ApplicationText { get; set; } = "";

    // Entry date
    public DateTime EntryDate { get; set; }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 6: Mappers

**Files:**
- Create: `Quartermaster.Data/Chapters/ChapterMapper.cs`
- Create: `Quartermaster.Data/MembershipApplications/MembershipApplicationMapper.cs`

- [ ] **Step 1: Create ChapterMapper**

Create `Quartermaster.Data/Chapters/ChapterMapper.cs`:

```csharp
using Quartermaster.Api.Chapters;
using Riok.Mapperly.Abstractions;

namespace Quartermaster.Data.Chapters;

[Mapper]
public static partial class ChapterMapper {
    public static partial ChapterDTO ToDto(this Chapter chapter);
}
```

- [ ] **Step 2: Create MembershipApplicationMapper**

Create `Quartermaster.Data/MembershipApplications/MembershipApplicationMapper.cs`:

```csharp
using Quartermaster.Api.MembershipApplications;
using Riok.Mapperly.Abstractions;

namespace Quartermaster.Data.MembershipApplications;

[Mapper]
public static partial class MembershipApplicationMapper {
    [MapperIgnoreSource(nameof(MembershipApplicationDTO.DueSelection))]
    public static partial MembershipApplication FromDto(MembershipApplicationDTO dto);
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 7: Server Endpoints

**Files:**
- Create: `Quartermaster.Server/Chapters/ChapterForDivisionEndpoint.cs`
- Create: `Quartermaster.Server/Chapters/ChapterListEndpoint.cs`
- Create: `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs`

- [ ] **Step 1: Create ChapterForDivisionEndpoint**

Create `Quartermaster.Server/Chapters/ChapterForDivisionEndpoint.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Chapters;

public class ChapterForDivisionRequest {
    public Guid DivisionId { get; set; }
}

public class ChapterForDivisionEndpoint : Endpoint<ChapterForDivisionRequest, ChapterDTO> {
    private readonly ChapterRepository _chapterRepository;
    private readonly AdministrativeDivisionRepository _adminDivRepository;

    public ChapterForDivisionEndpoint(ChapterRepository chapterRepository,
        AdministrativeDivisionRepository adminDivRepository) {
        _chapterRepository = chapterRepository;
        _adminDivRepository = adminDivRepository;
    }

    public override void Configure() {
        Get("/api/chapters/for-division/{DivisionId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterForDivisionRequest req, CancellationToken ct) {
        var chapter = _chapterRepository.FindForDivision(req.DivisionId, _adminDivRepository);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        await SendAsync(chapter.ToDto(), cancellation: ct);
    }
}
```

- [ ] **Step 2: Create ChapterListEndpoint**

Create `Quartermaster.Server/Chapters/ChapterListEndpoint.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Chapters;

public class ChapterListEndpoint : EndpointWithoutRequest<List<ChapterDTO>> {
    private readonly ChapterRepository _chapterRepository;

    public ChapterListEndpoint(ChapterRepository chapterRepository) {
        _chapterRepository = chapterRepository;
    }

    public override void Configure() {
        Get("/api/chapters");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var chapters = _chapterRepository.GetAll();
        await SendAsync(chapters.Select(c => c.ToDto()).ToList(), cancellation: ct);
    }
}
```

- [ ] **Step 3: Create MembershipApplicationCreateEndpoint**

Create `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Server.MembershipApplications;

public class MembershipApplicationCreateEndpoint : Endpoint<MembershipApplicationDTO> {
    private readonly MembershipApplicationRepository _applicationRepository;
    private readonly DueSelectionRepository _dueSelectionRepository;

    public MembershipApplicationCreateEndpoint(
        MembershipApplicationRepository applicationRepository,
        DueSelectionRepository dueSelectionRepository) {
        _applicationRepository = applicationRepository;
        _dueSelectionRepository = dueSelectionRepository;
    }

    public override void Configure() {
        Post("/api/membershipapplications");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MembershipApplicationDTO req, CancellationToken ct) {
        // Create the due selection first
        Guid? dueSelectionId = null;
        if (req.DueSelection != null) {
            var dueSelection = DueSelectionMapper.FromDto(req.DueSelection);
            _dueSelectionRepository.Create(dueSelection);
            dueSelectionId = dueSelection.Id;
        }

        // Create the membership application
        var application = MembershipApplicationMapper.FromDto(req);
        application.DueSelectionId = dueSelectionId;
        application.SubmittedAt = DateTime.UtcNow;
        _applicationRepository.Create(application);

        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 8: Membership Application Entry State & AppState Registration

**Files:**
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/MembershipApplicationEntryState.cs`
- Modify: `Quartermaster.Blazor/Services/AppStateService.cs`

- [ ] **Step 1: Create MembershipApplicationEntryState**

Create `Quartermaster.Blazor/Pages/MembershipApplication/MembershipApplicationEntryState.cs`:

```csharp
using System;
using Quartermaster.Blazor.Abstract;
using Quartermaster.Blazor.Pages.DueSelector;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public class MembershipApplicationEntryState : EntryStateBase {
    // Personal data
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public string Citizenship { get; set; } = "deutsch";
    public string EMail { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    // Address
    public string AddressStreet { get; set; } = "";
    public string AddressHouseNbr { get; set; } = "";
    public string AddressPostCode { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public Guid? AddressAdministrativeDivisionId { get; set; }

    // Chapter
    public Guid? ChapterId { get; set; }
    public string? ChapterName { get; set; }

    // Dues — embedded, reuses the existing entry state
    public DueSelectorEntryState DuesState { get; set; } = new();

    // Declarations
    public bool ConformityDeclarationAccepted { get; set; }
    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }
    public string ApplicationText { get; set; } = "";

    // Entry date
    public DateTime EntryDate { get; set; } = DateTime.Today;
}
```

- [ ] **Step 2: Register in AppStateService**

In `Quartermaster.Blazor/Services/AppStateService.cs`, add to the constructor:

```csharp
SupplementEntryState<MembershipApplicationEntryState>();
```

Add the using:
```csharp
using Quartermaster.Blazor.Pages.MembershipApplication;
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 9: Blazor Page — Personal Data (Step 1)

**Files:**
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/PersonalData.razor`
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/PersonalData.razor.cs`

- [ ] **Step 1: Create PersonalData.razor**

```razor
@page "/MembershipApplication/PersonalData"

<div class="mb-3">
    <h3>Mitgliedsantrag - Persönliche Daten</h3>
</div>

@if (EntryState == null)
    return;

<div class="mb-3 d-flex flex-row">
    <div class="flex-grow-1 pe-3">
        <label class="form-label">Vorname <RequiredStar /></label>
        <input type="text" class="form-control" @bind=EntryState.FirstName @bind:event="oninput" />
    </div>
    <div class="flex-grow-1">
        <label class="form-label">Nachname <RequiredStar /></label>
        <input type="text" class="form-control" @bind=EntryState.LastName @bind:event="oninput" />
    </div>
</div>

<div class="mb-3">
    <label class="form-label">Geburtsdatum <RequiredStar /></label>
    <p class="text-secondary small">Das Mindestalter liegt bei 14 Jahren.</p>
    <input type="date" class="form-control" @bind=EntryState.DateOfBirth />
</div>

<div class="mb-3">
    <label class="form-label">Staatsangehörigkeit <RequiredStar /></label>
    <p class="text-secondary small">Die Deutsche Staatsangehörigkeit ist nicht notwendig.</p>
    <input type="text" class="form-control" @bind=EntryState.Citizenship @bind:event="oninput" />
</div>

<div class="mb-3">
    <label class="form-label">E-Mail Adresse <RequiredStar /></label>
    <input type="email" class="form-control" @bind=EntryState.EMail @bind:event="oninput" />
</div>

<div class="mb-3">
    <label class="form-label">Telefonnummer <RequiredStar /></label>
    <input type="tel" class="form-control" @bind=EntryState.PhoneNumber @bind:event="oninput" />
</div>

<div class="d-flex justify-content-end">
    <a class="btn btn-primary @(CanContinue() ? "" : "disabled")"
       href="/MembershipApplication/Address">Weiter</a>
</div>
```

- [ ] **Step 2: Create PersonalData.razor.cs**

```csharp
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class PersonalData {
    [Inject]
    public required AppStateService AppState { get; set; }

    private MembershipApplicationEntryState? EntryState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
    }

    private bool CanContinue() {
        if (EntryState == null) return false;
        if (string.IsNullOrEmpty(EntryState.FirstName)) return false;
        if (string.IsNullOrEmpty(EntryState.LastName)) return false;
        if (EntryState.DateOfBirth == null) return false;
        if (string.IsNullOrEmpty(EntryState.Citizenship)) return false;
        if (string.IsNullOrEmpty(EntryState.EMail)) return false;
        if (string.IsNullOrEmpty(EntryState.PhoneNumber)) return false;
        return true;
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 10: Blazor Page — Address & Chapter (Step 2)

**Files:**
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/AddressAndChapter.razor`
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/AddressAndChapter.razor.cs`

- [ ] **Step 1: Create AddressAndChapter.razor**

```razor
@page "/MembershipApplication/Address"
@using Quartermaster.Api.AdministrativeDivisions
@using Quartermaster.Api.Chapters

<div class="mb-3">
    <h3>Mitgliedsantrag - Adresse und Gliederung</h3>
</div>

@if (EntryState == null)
    return;

<div class="mb-3 d-flex flex-row">
    <div class="flex-grow-1 pe-3">
        <label class="form-label">Straße <RequiredStar /></label>
        <input type="text" class="form-control" @bind=EntryState.AddressStreet @bind:event="oninput" />
    </div>
    <div style="width: 8em;">
        <label class="form-label">Hausnr. <RequiredStar /></label>
        <input type="text" class="form-control" @bind=EntryState.AddressHouseNbr @bind:event="oninput" />
    </div>
</div>

<div class="mb-3 d-flex flex-row">
    <div style="width: 10em;" class="pe-3">
        <label class="form-label">PLZ <RequiredStar /></label>
        <input type="text" class="form-control" @bind=EntryState.AddressPostCode @bind:event="oninput"
               @onblur="OnPostCodeChanged" />
    </div>
    <div class="flex-grow-1">
        <label class="form-label">Ort <RequiredStar /></label>
        <input type="text" class="form-control" @bind=EntryState.AddressCity @bind:event="oninput" />
    </div>
</div>

@if (MatchingDivisions != null && MatchingDivisions.Count > 0) {
    <div class="mb-3">
        <label class="form-label">Gemeinde</label>
        <select class="form-select" @onchange="OnDivisionSelected">
            <option value="">- Bitte auswählen -</option>
            @foreach (var div in MatchingDivisions) {
                <option value="@div.Id" selected="@(EntryState.AddressAdministrativeDivisionId == div.Id)">
                    @div.Name
                </option>
            }
        </select>
    </div>
}

@if (EntryState.ChapterName != null) {
    <div class="mb-3">
        <div class="card">
            <div class="card-body">
                <h5>Zugeordnete Gliederung</h5>
                <p class="card-text mb-0">@EntryState.ChapterName</p>
            </div>
        </div>
    </div>
}

<div class="d-flex justify-content-between">
    <a href="/MembershipApplication/PersonalData" class="btn btn-primary">Zurück</a>
    <a class="btn btn-primary @(CanContinue() ? "" : "disabled")"
       href="/MembershipApplication/DuesTypeSelection">Weiter</a>
</div>
```

- [ ] **Step 2: Create AddressAndChapter.razor.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.Chapters;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class AddressAndChapter {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required HttpClient Http { get; set; }

    private MembershipApplicationEntryState? EntryState;
    private List<AdministrativeDivisionDTO>? MatchingDivisions;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
    }

    private async Task OnPostCodeChanged() {
        if (EntryState == null || string.IsNullOrWhiteSpace(EntryState.AddressPostCode))
            return;

        var response = await Http.GetFromJsonAsync<AdministrativeDivisionSearchResponse>(
            $"/api/administrativedivisions/search?query={Uri.EscapeDataString(EntryState.AddressPostCode)}&page=1&pageSize=50");

        if (response != null) {
            // Filter to Gemeinde level (depth 7) for best chapter assignment
            MatchingDivisions = response.Items.FindAll(d => d.Depth == 7);
            if (MatchingDivisions.Count == 0)
                MatchingDivisions = response.Items;
        }

        StateHasChanged();
    }

    private async Task OnDivisionSelected(ChangeEventArgs e) {
        if (EntryState == null) return;

        if (Guid.TryParse(e.Value?.ToString(), out var divisionId)) {
            EntryState.AddressAdministrativeDivisionId = divisionId;
            await LookupChapter(divisionId);
        } else {
            EntryState.AddressAdministrativeDivisionId = null;
            EntryState.ChapterId = null;
            EntryState.ChapterName = null;
        }
        StateHasChanged();
    }

    private async Task LookupChapter(Guid divisionId) {
        if (EntryState == null) return;

        try {
            var chapter = await Http.GetFromJsonAsync<ChapterDTO>(
                $"/api/chapters/for-division/{divisionId}");
            if (chapter != null) {
                EntryState.ChapterId = chapter.Id;
                EntryState.ChapterName = chapter.Name;
            }
        } catch (HttpRequestException) {
            EntryState.ChapterId = null;
            EntryState.ChapterName = null;
        }
    }

    private bool CanContinue() {
        if (EntryState == null) return false;
        if (string.IsNullOrEmpty(EntryState.AddressStreet)) return false;
        if (string.IsNullOrEmpty(EntryState.AddressHouseNbr)) return false;
        if (string.IsNullOrEmpty(EntryState.AddressPostCode)) return false;
        if (string.IsNullOrEmpty(EntryState.AddressCity)) return false;
        return true;
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 11: Blazor Page — Dues Type Selection (Step 3, wraps existing)

This page is a thin wrapper that syncs the `MembershipApplicationEntryState.DuesState` into `AppStateService` as the `DueSelectorEntryState`, so the existing dues pages work unchanged. It pre-fills the name/email from the application state and provides navigation links that return to the membership flow.

**Files:**
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/DuesTypeSelection.razor`
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/DuesTypeSelection.razor.cs`

- [ ] **Step 1: Create DuesTypeSelection.razor**

```razor
@page "/MembershipApplication/DuesTypeSelection"

<div class="mb-3">
    <h3>Mitgliedsantrag - Beitragseinstufung</h3>
    <p class="text-secondary">Bitte stufe deinen Mitgliedsbeitrag ein. Nach der Einstufung wirst du zum nächsten Schritt weitergeleitet.</p>
</div>

@if (EntryState == null)
    return;

<div class="mb-2">
    <CardLink HRef="/DueSelector/SelectOnePercentYearlyPay" OnNavigate=@(() => SelectDueType(SelectedValuation.OnePercentYearlyPay))>
        <span>Einstufung auf 1% des Jahreseinkommens</span>
        <span class="fw-bold">Empfohlen</span>
    </CardLink>
</div>

<div class="mb-2">
    <CardLink HRef="/DueSelector/SelectByMonthlyPay" OnNavigate=@(() => SelectDueType(SelectedValuation.MonthlyPayGroup))>
        <span>Einstufung nach Monatseinkommen</span>
    </CardLink>
</div>

<div class="mb-2">
    <CardLink HRef="@($"/DueSelector/PaymentOptionSelection/{System.Net.WebUtility.UrlEncode("/MembershipApplication/DuesTypeSelection")}")"
              OnNavigate=@(() => HandleUnderage())>
        <span>Ich bin noch nicht 18 Jahre alt</span><br />
        <span>Unter 18 zahlst du automatisch nur 12€ im Jahr</span>
    </CardLink>
</div>

<div class="mb-3">
    <CardLink HRef="/DueSelector/SelectReduced" OnNavigate=@(() => SelectDueType(SelectedValuation.Reduced))>
        <span>Ich möchte den geminderten Beitrag beantragen</span>
    </CardLink>
</div>

<div>
    <a href="/MembershipApplication/Address" class="btn btn-primary">Zurück</a>
</div>
```

- [ ] **Step 2: Create DuesTypeSelection.razor.cs**

```csharp
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Pages.DueSelector;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class DuesTypeSelection {
    [Inject]
    public required AppStateService AppState { get; set; }

    private MembershipApplicationEntryState? EntryState;
    private DueSelectorEntryState? DuesState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
        DuesState = AppState.GetEntryState<DueSelectorEntryState>();
        SyncToDuesState();
    }

    private void SyncToDuesState() {
        if (EntryState == null || DuesState == null) return;

        // Pre-fill dues state from application
        DuesState.FirstName = EntryState.FirstName;
        DuesState.LastName = EntryState.LastName;
        DuesState.EMail = EntryState.EMail;
    }

    private void SelectDueType(SelectedValuation valuation) {
        if (DuesState == null) return;
        DuesState.SelectedValuation = valuation;
    }

    private void HandleUnderage() {
        if (DuesState == null) return;
        DuesState.SelectedValuation = SelectedValuation.Underage;
        DuesState.SelectedDue = 12;
    }
}
```

Note: The existing DueSelector pages navigate to `/DueSelector/Summary/{ReturnUrl}` at the end, and the Summary page submits the dues independently. For the membership flow, the dues summary page's "Zurück" URL chain will eventually lead back to `/MembershipApplication/DuesTypeSelection`. The user completes the full dues flow (including payment options and dues summary/submission), then can navigate to Declarations. We'll add a "Weiter zum Mitgliedsantrag" link in the next task.

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 12: Blazor Page — Declarations (Step 4)

**Files:**
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/Declarations.razor`
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/Declarations.razor.cs`

- [ ] **Step 1: Create Declarations.razor**

```razor
@page "/MembershipApplication/Declarations"

<div class="mb-3">
    <h3>Mitgliedsantrag - Erklärungen</h3>
</div>

@if (EntryState == null)
    return;

<div class="mb-3">
    <Checkbox @bind-Value=EntryState.ConformityDeclarationAccepted>
        <strong>Konformitätserklärung</strong> <RequiredStar />
    </Checkbox>
    <p class="text-secondary small ms-4">
        Ich erkenne die Grundsätze, politischen Ziele und die Satzung der Piratenpartei Deutschland an.
    </p>
</div>

<div class="mb-3">
    <Checkbox @bind-Value=NoPriorDeclinedApplication>
        Es wurde in der Vergangenheit noch kein Mitgliedsantrag bei einer Gliederung der Piratenpartei Deutschland abgelehnt.
    </Checkbox>
    <p class="text-secondary small ms-4">
        Wenn du dies nicht bestätigen kannst, gib uns in der Textbox weiter unten bitte die ablehnende Gliederung und das Datum der Ablehnung an.
    </p>
</div>

<div class="mb-3">
    <Checkbox @bind-Value=EntryState.IsMemberOfAnotherParty>
        Ich bin bereits Mitglied in einer anderen Partei
    </Checkbox>
    <p class="text-secondary small ms-4">
        Die gleichzeitige Mitgliedschaft in der Piratenpartei Deutschland und bei einer anderen Partei oder
        Wählergruppe ist nicht ausgeschlossen. Die Mitgliedschaft in einer Organisation oder Vereinigung,
        deren Zielsetzung den Zielen der Piratenpartei Deutschland widerspricht, ist nicht zulässig.
    </p>
</div>

<div class="mb-3">
    <label class="form-label">Eintritt zum</label>
    <input type="date" class="form-control" @bind=EntryState.EntryDate />
</div>

<div class="mb-3">
    <label class="form-label">...was Du uns noch sagen möchtest:</label>
    <textarea class="form-control" rows="4" @bind=EntryState.ApplicationText @bind:event="oninput"
              maxlength="2048"></textarea>
</div>

<div class="d-flex justify-content-between">
    <a href="/MembershipApplication/DuesTypeSelection" class="btn btn-primary">Zurück</a>
    <a class="btn btn-primary @(CanContinue() ? "" : "disabled")"
       href="/MembershipApplication/Summary">Weiter</a>
</div>
```

- [ ] **Step 2: Create Declarations.razor.cs**

```csharp
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class Declarations {
    [Inject]
    public required AppStateService AppState { get; set; }

    private MembershipApplicationEntryState? EntryState;

    // Inverted for UX: checkbox says "no prior rejection", but we store HasPriorDeclinedApplication
    private bool NoPriorDeclinedApplication {
        get => EntryState != null && !EntryState.HasPriorDeclinedApplication;
        set { if (EntryState != null) EntryState.HasPriorDeclinedApplication = !value; }
    }

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
    }

    private bool CanContinue() {
        if (EntryState == null) return false;
        if (!EntryState.ConformityDeclarationAccepted) return false;
        return true;
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 13: Blazor Page — Application Summary (Step 5)

**Files:**
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/ApplicationSummary.razor`
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/ApplicationSummary.razor.cs`

- [ ] **Step 1: Create ApplicationSummary.razor**

```razor
@page "/MembershipApplication/Summary"
@using System.Globalization
@using Quartermaster.Blazor.Pages.DueSelector

<div class="mb-3">
    <h3>Mitgliedsantrag - Zusammenfassung</h3>
</div>

@if (EntryState == null)
    return;

<div class="card mb-3">
    <div class="card-body">
        <h5>Persönliche Daten</h5>
        <ul class="list-group list-group-flush">
            <li class="list-group-item"><strong>Name:</strong> @EntryState.FirstName @EntryState.LastName</li>
            <li class="list-group-item"><strong>Geburtsdatum:</strong> @EntryState.DateOfBirth?.ToString("dd.MM.yyyy")</li>
            <li class="list-group-item"><strong>Staatsangehörigkeit:</strong> @EntryState.Citizenship</li>
            <li class="list-group-item"><strong>E-Mail:</strong> @EntryState.EMail</li>
            <li class="list-group-item"><strong>Telefon:</strong> @EntryState.PhoneNumber</li>
        </ul>
    </div>
</div>

<div class="card mb-3">
    <div class="card-body">
        <h5>Adresse</h5>
        <p class="card-text">
            @EntryState.AddressStreet @EntryState.AddressHouseNbr<br />
            @EntryState.AddressPostCode @EntryState.AddressCity
        </p>
        @if (EntryState.ChapterName != null) {
            <p class="card-text"><strong>Gliederung:</strong> @EntryState.ChapterName</p>
        }
    </div>
</div>

<div class="card mb-3">
    <div class="card-body">
        <h5>Beitrag</h5>
        @{ var dues = DuesState; }
        @if (dues != null && dues.SelectedDue > 0) {
            <p class="card-text">
                Jahresbeitrag: <strong>@dues.SelectedDue.ToString("C2", CultureInfo.GetCultureInfo("de-de"))</strong>
            </p>
        } else {
            <p class="card-text text-warning">Beitrag noch nicht eingestuft.</p>
        }
    </div>
</div>

<div class="card mb-3">
    <div class="card-body">
        <h5>Erklärungen</h5>
        <ul class="list-group list-group-flush">
            <li class="list-group-item">
                Konformitätserklärung: @(EntryState.ConformityDeclarationAccepted ? "Akzeptiert" : "Nicht akzeptiert")
            </li>
            @if (EntryState.HasPriorDeclinedApplication) {
                <li class="list-group-item text-warning">Früherer Antrag wurde abgelehnt</li>
            }
            @if (EntryState.IsMemberOfAnotherParty) {
                <li class="list-group-item">Mitglied einer anderen Partei</li>
            }
        </ul>
        @if (!string.IsNullOrEmpty(EntryState.ApplicationText)) {
            <p class="card-text mt-2"><em>@EntryState.ApplicationText</em></p>
        }
    </div>
</div>

<div class="card mb-3">
    <div class="card-body">
        <p class="card-text small text-secondary">
            Die Piratenpartei Deutschland verarbeitet die in diesem Aufnahmeantrag enthaltenen Angaben zur Person
            für ausschließlich interne Zwecke der Partei. Mit diesem Antrag auf Mitgliedschaft in der Partei
            erteilst du die nach Art. 6 abs. 1 lit. b) der Datenschutz-Grundverordnung (DSGVO) notwendige Einwilligung.
        </p>
    </div>
</div>

<div class="mb-3">
    <h3>Sind alle Daten korrekt?</h3>
</div>

<div class="mb-2">
    <CardButton OnClick=@Submit>
        <span>Mitgliedsantrag absenden</span>
    </CardButton>
</div>

<div>
    <a href="/MembershipApplication/Declarations" class="btn btn-primary">Zurück</a>
</div>
```

- [ ] **Step 2: Create ApplicationSummary.razor.cs**

```csharp
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Blazor.Pages.DueSelector;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class ApplicationSummary {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private MembershipApplicationEntryState? EntryState;
    private DueSelectorEntryState? DuesState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
        DuesState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private async Task Submit() {
        if (EntryState == null)
            throw new UnreachableException();

        var dto = new MembershipApplicationDTO {
            FirstName = EntryState.FirstName,
            LastName = EntryState.LastName,
            DateOfBirth = EntryState.DateOfBirth ?? DateTime.MinValue,
            Citizenship = EntryState.Citizenship,
            EMail = EntryState.EMail,
            PhoneNumber = EntryState.PhoneNumber,
            AddressStreet = EntryState.AddressStreet,
            AddressHouseNbr = EntryState.AddressHouseNbr,
            AddressPostCode = EntryState.AddressPostCode,
            AddressCity = EntryState.AddressCity,
            AddressAdministrativeDivisionId = EntryState.AddressAdministrativeDivisionId,
            ChapterId = EntryState.ChapterId,
            DueSelection = DuesState?.ToDTO(),
            ConformityDeclarationAccepted = EntryState.ConformityDeclarationAccepted,
            HasPriorDeclinedApplication = EntryState.HasPriorDeclinedApplication,
            IsMemberOfAnotherParty = EntryState.IsMemberOfAnotherParty,
            ApplicationText = EntryState.ApplicationText,
            EntryDate = EntryState.EntryDate
        };

        var okResponse = false;
        try {
            var result = await Http.PostAsJsonAsync("/api/membershipapplications", dto);
            okResponse = result.IsSuccessStatusCode;
        } catch (HttpRequestException) { }

        if (okResponse) {
            AppState.ResetEntryState<MembershipApplicationEntryState>();
            AppState.ResetEntryState<DueSelectorEntryState>();
            NavigationManager.NavigateTo("/");
            ToastService.Toast("Dein Mitgliedsantrag wurde erfolgreich eingereicht!", "success");
        } else {
            ToastService.Toast("Es ist ein Fehler aufgetreten, bitte versuche es später nochmal erneut.", "danger");
        }
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 14: Navigation & Final Wiring

**Files:**
- Modify: `Quartermaster.Blazor/Layout/MainLayout.razor`

- [ ] **Step 1: Add Mitgliedsantrag to navigation**

In `MainLayout.razor`, add the membership application link under the Mitgliedsportal dropdown, after the Beitragseinstufung link:

```razor
<li><a class="dropdown-item" href="/MembershipApplication/PersonalData">Mitgliedsantrag</a></li>
```

The full dropdown section becomes:

```razor
<NavMenuDropdown>
    <ButtonContent>Mitgliedsportal</ButtonContent>
    <DropdownContent>
        <li><a class="dropdown-item" href="/DueSelector/UserDataInput">Beitragseinstufung</a></li>
        <li><a class="dropdown-item" href="/MembershipApplication/PersonalData">Mitgliedsantrag</a></li>
    </DropdownContent>
</NavMenuDropdown>
```

- [ ] **Step 2: Build full solution and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Start server with fresh DB and verify**

```bash
# Clean, rebuild, fresh DB
rm -rf Quartermaster.Api/obj Quartermaster.Data/obj Quartermaster.Server/obj Quartermaster.Blazor/obj
mysql -u root -e "DROP DATABASE IF EXISTS quartermaster; CREATE DATABASE quartermaster;"
dotnet watch --project Quartermaster.Server
```

Verify:
1. Server starts successfully
2. Chapters table has 17 entries (1 Bundesverband + 16 Landesverbände)
3. Navigate to `/MembershipApplication/PersonalData` in browser — wizard loads
4. Existing `/DueSelector/UserDataInput` flow still works independently

---

## Task 15: Update Translations Documentation

**Files:**
- Modify: `Quartermaster.Documentation/Translations.md`

- [ ] **Step 1: Add membership application terms**

Add this section to `Translations.md`:

```markdown
## Membership Application (Mitgliedsantrag)

| German | English | Notes |
|---|---|---|
| Mitgliedsantrag | Membership Application | |
| Persönliche Daten | Personal Data | |
| Geburtsdatum | Date of Birth | |
| Staatsangehörigkeit | Citizenship | |
| Telefonnummer | Phone Number | |
| Adresse | Address | |
| Gliederung | Chapter | Organizational unit within the party |
| Zugeordnete Gliederung | Assigned Chapter | |
| Straße | Street | |
| Hausnr. | House Number | Abbreviated in UI |
| PLZ | Post Code | Abbreviation of Postleitzahl |
| Ort | City / Place | |
| Erklärungen | Declarations | |
| Konformitätserklärung | Conformity Declaration | Acceptance of party principles and statutes |
| Eintritt zum | Entry Date | Date of joining |
| Mitgliedsantrag absenden | Submit Membership Application | |
```

---

## Summary of Changes

| Layer | Files Created | Files Modified |
|---|---|---|
| Data | ChapterRepository.cs, MembershipApplicationRepository.cs, MembershipApplicationMapper.cs, ChapterMapper.cs | Chapter.cs, MembershipApplication.cs, M001 migration, DbContext.cs, AdministrativeDivisionRepository.cs |
| API | ChapterDTO.cs, MembershipApplicationDTO.cs | — |
| Server | ChapterForDivisionEndpoint.cs, ChapterListEndpoint.cs, MembershipApplicationCreateEndpoint.cs | — |
| Blazor | MembershipApplicationEntryState.cs, PersonalData.razor/.cs, AddressAndChapter.razor/.cs, DuesTypeSelection.razor/.cs, Declarations.razor/.cs, ApplicationSummary.razor/.cs | AppStateService.cs, MainLayout.razor |
| Docs | — | Translations.md |
