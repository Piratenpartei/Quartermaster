# Administration Feature Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add administration pages for processing membership applications and due selections, with chapter-scoped visibility (parent chapters see child chapter data).

**Architecture:** Add status/processing fields to MembershipApplication and DueSelection entities (in M001). Auto-approve non-reduced dues on submission. Add ChapterRepository.GetDescendantIds() for hierarchy queries. Build paginated admin list endpoints (temporarily AllowAnonymous). Build Blazor admin pages with status filters, approve/reject actions. New permission identifiers seeded for future auth enforcement.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB, Blazor WASM, Bootstrap 5

---

## File Structure

### Data Model Changes (modify existing)
- `Quartermaster.Data/MembershipApplications/MembershipApplication.cs` — add Status, ProcessedByUserId, ProcessedAt
- `Quartermaster.Data/DueSelector/DueSelection.cs` — add Status, ProcessedByUserId, ProcessedAt
- `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs` — add new columns to both tables
- `Quartermaster.Data/MembershipApplications/MembershipApplicationRepository.cs` — add query/update methods
- `Quartermaster.Data/DueSelector/DueSelectionRepository.cs` — add query/update methods
- `Quartermaster.Data/Chapters/ChapterRepository.cs` — add GetDescendantIds()
- `Quartermaster.Api/PermissionIdentifier.cs` — add admin permission identifiers
- `Quartermaster.Data/Permissions/PermissionRepository.cs` — seed new permissions
- `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs` — set initial status

### API DTOs (new + modify)
- `Quartermaster.Api/MembershipApplications/MembershipApplicationAdminDTO.cs` — admin list item DTO
- `Quartermaster.Api/MembershipApplications/MembershipApplicationListRequest.cs` — list query params
- `Quartermaster.Api/MembershipApplications/MembershipApplicationListResponse.cs` — paginated response
- `Quartermaster.Api/DueSelector/DueSelectionAdminDTO.cs` — admin list item DTO
- `Quartermaster.Api/DueSelector/DueSelectionListRequest.cs` — list query params
- `Quartermaster.Api/DueSelector/DueSelectionListResponse.cs` — paginated response

### Server Endpoints (new)
- `Quartermaster.Server/Admin/MembershipApplicationListEndpoint.cs` — GET list
- `Quartermaster.Server/Admin/MembershipApplicationProcessEndpoint.cs` — POST approve/reject
- `Quartermaster.Server/Admin/DueSelectionListEndpoint.cs` — GET list
- `Quartermaster.Server/Admin/DueSelectionProcessEndpoint.cs` — POST approve/reject

### Blazor Pages (new)
- `Quartermaster.Blazor/Pages/Administration/MembershipApplicationAdmin.razor` + `.cs`
- `Quartermaster.Blazor/Pages/Administration/DueSelectionAdmin.razor` + `.cs`
- `Quartermaster.Blazor/Layout/MainLayout.razor` — add admin nav links

---

## Task 1: Add Status Fields to Entities + Migration

**Files:**
- Modify: `Quartermaster.Data/MembershipApplications/MembershipApplication.cs`
- Modify: `Quartermaster.Data/DueSelector/DueSelection.cs`
- Modify: `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`

- [ ] **Step 1: Add status enum and fields to MembershipApplication**

Add at the end of `MembershipApplication.cs`, before the closing brace:

```csharp
    // Processing
    public ApplicationStatus Status { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public enum ApplicationStatus {
    Pending,
    Approved,
    Rejected
}
```

Remove the old closing brace of the class — the enum goes after it. Full tail of the file becomes:

```csharp
    // Entry date
    public DateTime EntryDate { get; set; }
    public DateTime SubmittedAt { get; set; }

    // Processing
    public ApplicationStatus Status { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public enum ApplicationStatus {
    Pending,
    Approved,
    Rejected
}
```

- [ ] **Step 2: Add status enum and fields to DueSelection**

Add to `DueSelection.cs`, before the closing brace of the class, after `PaymentSchedule`:

```csharp
    // Processing
    public DueSelectionStatus Status { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
```

Add enum after the class (before DueSelectionMapper):

```csharp
public enum DueSelectionStatus {
    Pending,
    Approved,
    Rejected,
    AutoApproved
}
```

- [ ] **Step 3: Update M001 migration — MembershipApplications columns**

In the MembershipApplications table creation, add after `SubmittedAt`:

```csharp
            .WithColumn(nameof(MembershipApplication.Status)).AsInt32()
            .WithColumn(nameof(MembershipApplication.ProcessedByUserId)).AsGuid().Nullable()
            .WithColumn(nameof(MembershipApplication.ProcessedAt)).AsDateTime().Nullable();
```

Add FK after the existing MembershipApplications FKs:

```csharp
        Create.ForeignKey("FK_MemberApps_ProcessedByUserId_Users_Id")
            .FromTable(MembershipApplication.TableName).ForeignColumn(nameof(MembershipApplication.ProcessedByUserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));
```

Add to Down():

```csharp
        Delete.ForeignKey("FK_MemberApps_ProcessedByUserId_Users_Id")
            .OnTable(MembershipApplication.TableName);
```

- [ ] **Step 4: Update M001 migration — DueSelections columns**

In the DueSelections table creation, add after `PaymentSchedule`:

```csharp
            .WithColumn(nameof(DueSelection.Status)).AsInt32()
            .WithColumn(nameof(DueSelection.ProcessedByUserId)).AsGuid().Nullable()
            .WithColumn(nameof(DueSelection.ProcessedAt)).AsDateTime().Nullable();
```

Add FK after `FK_DueSelections_UserId_User_Id`:

```csharp
        Create.ForeignKey("FK_DueSelections_ProcessedByUserId_Users_Id")
            .FromTable(DueSelection.TableName).ForeignColumn(nameof(DueSelection.ProcessedByUserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));
```

Add to Down():

```csharp
        Delete.ForeignKey("FK_DueSelections_ProcessedByUserId_Users_Id")
            .OnTable(DueSelection.TableName);
```

- [ ] **Step 5: Build and verify**

Run: `cd /media/SMB/Quartermaster && dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 2: Set Initial Status on Submission

**Files:**
- Modify: `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs`

- [ ] **Step 1: Set status when creating application and due selection**

In `MembershipApplicationCreateEndpoint.HandleAsync`, after creating the due selection, set its status:

```csharp
    public override async Task HandleAsync(MembershipApplicationDTO req, CancellationToken ct) {
        Guid? dueSelectionId = null;
        if (req.DueSelection != null) {
            var dueSelection = DueSelectionMapper.FromDto(req.DueSelection);

            // Auto-approve non-reduced dues, reduced needs manual approval
            dueSelection.Status = dueSelection.SelectedValuation == SelectedValuation.Reduced
                ? DueSelectionStatus.Pending
                : DueSelectionStatus.AutoApproved;

            _dueSelectionRepository.Create(dueSelection);
            dueSelectionId = dueSelection.Id;
        }

        var application = MembershipApplicationMapper.FromDto(req);
        application.DueSelectionId = dueSelectionId;
        application.SubmittedAt = DateTime.UtcNow;
        application.Status = ApplicationStatus.Pending;
        _applicationRepository.Create(application);

        await SendOkAsync(ct);
    }
```

Add using: `using Quartermaster.Data.DueSelector;` (for the enums) and `using Quartermaster.Data.MembershipApplications;` (for ApplicationStatus).

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 3: Chapter Hierarchy Query + Repository Query Methods

**Files:**
- Modify: `Quartermaster.Data/Chapters/ChapterRepository.cs`
- Modify: `Quartermaster.Data/MembershipApplications/MembershipApplicationRepository.cs`
- Modify: `Quartermaster.Data/DueSelector/DueSelectionRepository.cs`

- [ ] **Step 1: Add GetDescendantIds to ChapterRepository**

Add this method to `ChapterRepository`:

```csharp
    public List<Guid> GetDescendantIds(Guid chapterId) {
        var result = new List<Guid> { chapterId };
        var queue = new Queue<Guid>();
        queue.Enqueue(chapterId);

        while (queue.Count > 0) {
            var parentId = queue.Dequeue();
            var children = _context.Chapters
                .Where(c => c.ParentChapterId == parentId && c.Id != parentId)
                .Select(c => c.Id)
                .ToList();

            foreach (var childId in children) {
                result.Add(childId);
                queue.Enqueue(childId);
            }
        }

        return result;
    }
```

- [ ] **Step 2: Add query methods to MembershipApplicationRepository**

Replace the full content of `MembershipApplicationRepository.cs`:

```csharp
using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.MembershipApplications;

public class MembershipApplicationRepository {
    private readonly DbContext _context;

    public MembershipApplicationRepository(DbContext context) {
        _context = context;
    }

    public MembershipApplication? Get(Guid id)
        => _context.MembershipApplications.Where(a => a.Id == id).FirstOrDefault();

    public void Create(MembershipApplication application) => _context.Insert(application);

    public (List<MembershipApplication> Items, int TotalCount) List(
        List<Guid> chapterIds, ApplicationStatus? status, int page, int pageSize) {

        var q = _context.MembershipApplications.AsQueryable();

        if (chapterIds.Count > 0)
            q = q.Where(a => a.ChapterId != null && chapterIds.Contains(a.ChapterId.Value));

        if (status != null)
            q = q.Where(a => a.Status == status.Value);

        var totalCount = q.Count();
        var items = q.OrderByDescending(a => a.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public void UpdateStatus(Guid id, ApplicationStatus status, Guid? processedByUserId) {
        _context.MembershipApplications
            .Where(a => a.Id == id)
            .Set(a => a.Status, status)
            .Set(a => a.ProcessedByUserId, processedByUserId)
            .Set(a => a.ProcessedAt, DateTime.UtcNow)
            .Update();
    }
}
```

- [ ] **Step 3: Add query methods to DueSelectionRepository**

Replace the full content of `DueSelectionRepository.cs`:

```csharp
using LinqToDB;
using Quartermaster.Data.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.DueSelector;

public class DueSelectionRepository : RepositoryBase<DueSelection> {
    private readonly DbContext _context;

    public DueSelectionRepository(DbContext context) {
        _context = context;
    }

    public DueSelection? Get(Guid id)
        => _context.DueSelections.Where(d => d.Id == id).FirstOrDefault();

    public void Create(DueSelection selection) => _context.Insert(selection);

    public (List<DueSelection> Items, int TotalCount) List(
        DueSelectionStatus? status, int page, int pageSize) {

        var q = _context.DueSelections.AsQueryable();

        if (status != null)
            q = q.Where(d => d.Status == status.Value);

        var totalCount = q.Count();
        var items = q.OrderByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public void UpdateStatus(Guid id, DueSelectionStatus status, Guid? processedByUserId) {
        _context.DueSelections
            .Where(d => d.Id == id)
            .Set(d => d.Status, status)
            .Set(d => d.ProcessedByUserId, processedByUserId)
            .Set(d => d.ProcessedAt, DateTime.UtcNow)
            .Update();
    }
}
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 4: Permission Identifiers + Seeding

**Files:**
- Modify: `Quartermaster.Api/PermissionIdentifier.cs`
- Modify: `Quartermaster.Data/Permissions/PermissionRepository.cs`

- [ ] **Step 1: Add admin permission identifiers**

Add to `PermissionIdentifier.cs`:

```csharp
    public static readonly string ViewApplications = "applications_view";
    public static readonly string ProcessApplications = "applications_process";
    public static readonly string ViewDueSelections = "dueselections_view";
    public static readonly string ProcessDueSelections = "dueselections_process";
```

- [ ] **Step 2: Seed new permissions**

Add to `PermissionRepository.SupplementDefaults()`:

```csharp
        AddIfNotExists(PermissionIdentifier.ViewApplications, "Mitgliedsanträge Einsehen", false);
        AddIfNotExists(PermissionIdentifier.ProcessApplications, "Mitgliedsanträge Bearbeiten", false);
        AddIfNotExists(PermissionIdentifier.ViewDueSelections, "Beitragseinstufungen Einsehen", false);
        AddIfNotExists(PermissionIdentifier.ProcessDueSelections, "Beitragseinstufungen Bearbeiten", false);
```

Note: `Global = false` because these are chapter-scoped permissions.

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 5: Admin API DTOs

**Files:**
- Create: `Quartermaster.Api/MembershipApplications/MembershipApplicationAdminDTO.cs`
- Create: `Quartermaster.Api/MembershipApplications/MembershipApplicationListRequest.cs`
- Create: `Quartermaster.Api/MembershipApplications/MembershipApplicationListResponse.cs`
- Create: `Quartermaster.Api/DueSelector/DueSelectionAdminDTO.cs`
- Create: `Quartermaster.Api/DueSelector/DueSelectionListRequest.cs`
- Create: `Quartermaster.Api/DueSelector/DueSelectionListResponse.cs`

- [ ] **Step 1: Create MembershipApplication admin DTOs**

`MembershipApplicationAdminDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationAdminDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string EMail { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public int Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
```

`MembershipApplicationListRequest.cs`:

```csharp
using System;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationListRequest {
    public Guid? ChapterId { get; set; }
    public int? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
```

`MembershipApplicationListResponse.cs`:

```csharp
using System.Collections.Generic;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationListResponse {
    public List<MembershipApplicationAdminDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

- [ ] **Step 2: Create DueSelection admin DTOs**

`DueSelectionAdminDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.DueSelector;

public class DueSelectionAdminDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? EMail { get; set; }
    public decimal SelectedDue { get; set; }
    public decimal ReducedAmount { get; set; }
    public string ReducedJustification { get; set; } = "";
    public int SelectedValuation { get; set; }
    public int Status { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
```

`DueSelectionListRequest.cs`:

```csharp
namespace Quartermaster.Api.DueSelector;

public class DueSelectionListRequest {
    public int? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
```

`DueSelectionListResponse.cs`:

```csharp
using System.Collections.Generic;

namespace Quartermaster.Api.DueSelector;

public class DueSelectionListResponse {
    public List<DueSelectionAdminDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 6: Admin Server Endpoints

**Files:**
- Create: `Quartermaster.Server/Admin/MembershipApplicationListEndpoint.cs`
- Create: `Quartermaster.Server/Admin/MembershipApplicationProcessEndpoint.cs`
- Create: `Quartermaster.Server/Admin/DueSelectionListEndpoint.cs`
- Create: `Quartermaster.Server/Admin/DueSelectionProcessEndpoint.cs`

- [ ] **Step 1: Create MembershipApplicationListEndpoint**

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationListEndpoint
    : Endpoint<MembershipApplicationListRequest, MembershipApplicationListResponse> {

    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;

    public MembershipApplicationListEndpoint(
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo) {
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/admin/membershipapplications");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(MembershipApplicationListRequest req, CancellationToken ct) {
        var chapterIds = req.ChapterId.HasValue
            ? _chapterRepo.GetDescendantIds(req.ChapterId.Value)
            : new System.Collections.Generic.List<System.Guid>();

        ApplicationStatus? status = req.Status.HasValue
            ? (ApplicationStatus)req.Status.Value
            : null;

        var (items, totalCount) = _applicationRepo.List(chapterIds, status, req.Page, req.PageSize);

        // Build chapter name lookup
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var dtos = items.Select(a => new MembershipApplicationAdminDTO {
            Id = a.Id,
            FirstName = a.FirstName,
            LastName = a.LastName,
            EMail = a.EMail,
            AddressCity = a.AddressCity,
            ChapterId = a.ChapterId,
            ChapterName = a.ChapterId.HasValue && chapters.ContainsKey(a.ChapterId.Value)
                ? chapters[a.ChapterId.Value] : "",
            Status = (int)a.Status,
            SubmittedAt = a.SubmittedAt,
            ProcessedAt = a.ProcessedAt
        }).ToList();

        await SendAsync(new MembershipApplicationListResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
```

- [ ] **Step 2: Create MembershipApplicationProcessEndpoint**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationProcessRequest {
    public Guid Id { get; set; }
    public int Status { get; set; } // 1 = Approved, 2 = Rejected
}

public class MembershipApplicationProcessEndpoint : Endpoint<MembershipApplicationProcessRequest> {
    private readonly MembershipApplicationRepository _applicationRepo;

    public MembershipApplicationProcessEndpoint(MembershipApplicationRepository applicationRepo) {
        _applicationRepo = applicationRepo;
    }

    public override void Configure() {
        Post("/api/admin/membershipapplications/process");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(MembershipApplicationProcessRequest req, CancellationToken ct) {
        var application = _applicationRepo.Get(req.Id);
        if (application == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var status = (ApplicationStatus)req.Status;
        if (status != ApplicationStatus.Approved && status != ApplicationStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _applicationRepo.UpdateStatus(req.Id, status, null); // null userId until auth exists
        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 3: Create DueSelectionListEndpoint**

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;

namespace Quartermaster.Server.Admin;

public class DueSelectionListEndpoint
    : Endpoint<DueSelectionListRequest, DueSelectionListResponse> {

    private readonly DueSelectionRepository _dueSelectionRepo;

    public DueSelectionListEndpoint(DueSelectionRepository dueSelectionRepo) {
        _dueSelectionRepo = dueSelectionRepo;
    }

    public override void Configure() {
        Get("/api/admin/dueselections");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(DueSelectionListRequest req, CancellationToken ct) {
        DueSelectionStatus? status = req.Status.HasValue
            ? (DueSelectionStatus)req.Status.Value
            : null;

        var (items, totalCount) = _dueSelectionRepo.List(status, req.Page, req.PageSize);

        var dtos = items.Select(d => new DueSelectionAdminDTO {
            Id = d.Id,
            FirstName = d.FirstName,
            LastName = d.LastName,
            EMail = d.EMail,
            SelectedDue = d.SelectedDue,
            ReducedAmount = d.ReducedAmount,
            ReducedJustification = d.ReducedJustification,
            SelectedValuation = (int)d.SelectedValuation,
            Status = (int)d.Status,
            ProcessedAt = d.ProcessedAt
        }).ToList();

        await SendAsync(new DueSelectionListResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
```

- [ ] **Step 4: Create DueSelectionProcessEndpoint**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.DueSelector;

namespace Quartermaster.Server.Admin;

public class DueSelectionProcessRequest {
    public Guid Id { get; set; }
    public int Status { get; set; } // 1 = Approved, 2 = Rejected
}

public class DueSelectionProcessEndpoint : Endpoint<DueSelectionProcessRequest> {
    private readonly DueSelectionRepository _dueSelectionRepo;

    public DueSelectionProcessEndpoint(DueSelectionRepository dueSelectionRepo) {
        _dueSelectionRepo = dueSelectionRepo;
    }

    public override void Configure() {
        Post("/api/admin/dueselections/process");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(DueSelectionProcessRequest req, CancellationToken ct) {
        var selection = _dueSelectionRepo.Get(req.Id);
        if (selection == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var status = (DueSelectionStatus)req.Status;
        if (status != DueSelectionStatus.Approved && status != DueSelectionStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _dueSelectionRepo.UpdateStatus(req.Id, status, null); // null userId until auth exists
        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 7: Blazor Admin Page — Membership Applications

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/MembershipApplicationAdmin.razor`
- Create: `Quartermaster.Blazor/Pages/Administration/MembershipApplicationAdmin.razor.cs`

- [ ] **Step 1: Create MembershipApplicationAdmin.razor**

```razor
@page "/Administration/MembershipApplications"
@using Quartermaster.Api.MembershipApplications
@using Quartermaster.Api.Chapters
@using System.Globalization

<div class="mb-3">
    <h3>Verwaltung - Mitgliedsanträge</h3>
</div>

<div class="mb-3 d-flex gap-3">
    <div>
        <label class="form-label">Gliederung</label>
        <select class="form-select" @onchange="OnChapterChanged">
            <option value="">Alle Gliederungen</option>
            @if (Chapters != null) {
                @foreach (var chapter in Chapters) {
                    <option value="@chapter.Id">@chapter.Name</option>
                }
            }
        </select>
    </div>
    <div>
        <label class="form-label">Status</label>
        <select class="form-select" @onchange="OnStatusChanged">
            <option value="">Alle</option>
            <option value="0" selected>Ausstehend</option>
            <option value="1">Genehmigt</option>
            <option value="2">Abgelehnt</option>
        </select>
    </div>
</div>

@if (Loading) {
    <div class="d-flex justify-content-center my-4">
        <div class="spinner-border" role="status"></div>
    </div>
} else if (Response != null) {
    <div class="mb-2 text-secondary">
        @Response.TotalCount Anträge
    </div>

    <table class="table table-striped table-hover">
        <thead>
            <tr>
                <th>Name</th>
                <th>E-Mail</th>
                <th>Ort</th>
                <th>Gliederung</th>
                <th>Status</th>
                <th>Eingereicht</th>
                <th>Aktionen</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var app in Response.Items) {
                <tr>
                    <td>@app.FirstName @app.LastName</td>
                    <td>@app.EMail</td>
                    <td>@app.AddressCity</td>
                    <td>@app.ChapterName</td>
                    <td>
                        @if (app.Status == 0) {
                            <span class="badge bg-warning">Ausstehend</span>
                        } else if (app.Status == 1) {
                            <span class="badge bg-success">Genehmigt</span>
                        } else {
                            <span class="badge bg-danger">Abgelehnt</span>
                        }
                    </td>
                    <td>@app.SubmittedAt.ToString("dd.MM.yyyy HH:mm")</td>
                    <td>
                        @if (app.Status == 0) {
                            <button class="btn btn-sm btn-success me-1" @onclick="() => Process(app.Id, 1)">
                                <i class="bi bi-check-lg"></i>
                            </button>
                            <button class="btn btn-sm btn-danger" @onclick="() => Process(app.Id, 2)">
                                <i class="bi bi-x-lg"></i>
                            </button>
                        } else {
                            <span class="text-secondary">@app.ProcessedAt?.ToString("dd.MM.yyyy")</span>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>

    <Pagination CurrentPage="CurrentPage" TotalPages="TotalPages" OnPageChanged="GoToPage" />
}
```

- [ ] **Step 2: Create MembershipApplicationAdmin.razor.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MembershipApplicationAdmin {
    [Inject]
    public required HttpClient Http { get; set; }

    private List<ChapterDTO>? Chapters;
    private MembershipApplicationListResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private Guid? SelectedChapterId;
    private int? SelectedStatus = 0; // Default to Pending

    private int TotalPages => Response == null ? 0
        : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
        Chapters = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");
        await Search();
    }

    private async Task OnChapterChanged(ChangeEventArgs e) {
        SelectedChapterId = Guid.TryParse(e.Value?.ToString(), out var id) ? id : null;
        CurrentPage = 1;
        await Search();
    }

    private async Task OnStatusChanged(ChangeEventArgs e) {
        SelectedStatus = int.TryParse(e.Value?.ToString(), out var s) ? s : null;
        CurrentPage = 1;
        await Search();
    }

    private async Task GoToPage(int page) {
        CurrentPage = page;
        await Search();
    }

    private async Task Search() {
        Loading = true;
        StateHasChanged();

        var url = $"/api/admin/membershipapplications?page={CurrentPage}&pageSize={PageSize}";
        if (SelectedChapterId.HasValue)
            url += $"&chapterId={SelectedChapterId.Value}";
        if (SelectedStatus.HasValue)
            url += $"&status={SelectedStatus.Value}";

        Response = await Http.GetFromJsonAsync<MembershipApplicationListResponse>(url);

        Loading = false;
        StateHasChanged();
    }

    private async Task Process(Guid id, int status) {
        await Http.PostAsJsonAsync("/api/admin/membershipapplications/process",
            new { Id = id, Status = status });
        await Search();
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 8: Blazor Admin Page — Due Selections

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/DueSelectionAdmin.razor`
- Create: `Quartermaster.Blazor/Pages/Administration/DueSelectionAdmin.razor.cs`

- [ ] **Step 1: Create DueSelectionAdmin.razor**

```razor
@page "/Administration/DueSelections"
@using Quartermaster.Api.DueSelector
@using System.Globalization

<div class="mb-3">
    <h3>Verwaltung - Beitragseinstufungen</h3>
</div>

<div class="mb-3">
    <label class="form-label">Status</label>
    <select class="form-select" style="width: auto;" @onchange="OnStatusChanged">
        <option value="">Alle</option>
        <option value="0" selected>Ausstehend</option>
        <option value="1">Genehmigt</option>
        <option value="2">Abgelehnt</option>
        <option value="3">Automatisch genehmigt</option>
    </select>
</div>

@if (Loading) {
    <div class="d-flex justify-content-center my-4">
        <div class="spinner-border" role="status"></div>
    </div>
} else if (Response != null) {
    <div class="mb-2 text-secondary">
        @Response.TotalCount Einstufungen
    </div>

    <table class="table table-striped table-hover">
        <thead>
            <tr>
                <th>Name</th>
                <th>E-Mail</th>
                <th>Gewählter Beitrag</th>
                <th>Geminderter Betrag</th>
                <th>Begründung</th>
                <th>Status</th>
                <th>Aktionen</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var ds in Response.Items) {
                <tr>
                    <td>@ds.FirstName @ds.LastName</td>
                    <td>@ds.EMail</td>
                    <td>@ds.SelectedDue.ToString("C2", CultureInfo.GetCultureInfo("de-de"))</td>
                    <td>
                        @if (ds.SelectedValuation == 4) {
                            @ds.ReducedAmount.ToString("C2", CultureInfo.GetCultureInfo("de-de"))
                        } else {
                            <span class="text-secondary">—</span>
                        }
                    </td>
                    <td class="text-truncate" style="max-width: 250px;" title="@ds.ReducedJustification">
                        @(string.IsNullOrEmpty(ds.ReducedJustification) ? "—" : ds.ReducedJustification)
                    </td>
                    <td>
                        @if (ds.Status == 0) {
                            <span class="badge bg-warning">Ausstehend</span>
                        } else if (ds.Status == 1) {
                            <span class="badge bg-success">Genehmigt</span>
                        } else if (ds.Status == 2) {
                            <span class="badge bg-danger">Abgelehnt</span>
                        } else {
                            <span class="badge bg-info">Auto</span>
                        }
                    </td>
                    <td>
                        @if (ds.Status == 0) {
                            <button class="btn btn-sm btn-success me-1" @onclick="() => Process(ds.Id, 1)">
                                <i class="bi bi-check-lg"></i>
                            </button>
                            <button class="btn btn-sm btn-danger" @onclick="() => Process(ds.Id, 2)">
                                <i class="bi bi-x-lg"></i>
                            </button>
                        } else {
                            <span class="text-secondary">@ds.ProcessedAt?.ToString("dd.MM.yyyy")</span>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>

    <Pagination CurrentPage="CurrentPage" TotalPages="TotalPages" OnPageChanged="GoToPage" />
}
```

- [ ] **Step 2: Create DueSelectionAdmin.razor.cs**

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class DueSelectionAdmin {
    [Inject]
    public required HttpClient Http { get; set; }

    private DueSelectionListResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private int? SelectedStatus = 0; // Default to Pending

    private int TotalPages => Response == null ? 0
        : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
        await Search();
    }

    private async Task OnStatusChanged(ChangeEventArgs e) {
        SelectedStatus = int.TryParse(e.Value?.ToString(), out var s) ? s : null;
        CurrentPage = 1;
        await Search();
    }

    private async Task GoToPage(int page) {
        CurrentPage = page;
        await Search();
    }

    private async Task Search() {
        Loading = true;
        StateHasChanged();

        var url = $"/api/admin/dueselections?page={CurrentPage}&pageSize={PageSize}";
        if (SelectedStatus.HasValue)
            url += $"&status={SelectedStatus.Value}";

        Response = await Http.GetFromJsonAsync<DueSelectionListResponse>(url);

        Loading = false;
        StateHasChanged();
    }

    private async Task Process(Guid id, int status) {
        await Http.PostAsJsonAsync("/api/admin/dueselections/process",
            new { Id = id, Status = status });
        await Search();
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 9: Navigation + Translations

**Files:**
- Modify: `Quartermaster.Blazor/Layout/MainLayout.razor`
- Modify: `Quartermaster.Documentation/Translations.md`

- [ ] **Step 1: Add admin links to navigation**

In `MainLayout.razor`, in the Verwaltung dropdown, add after the existing Gemeindedatenbank links:

```razor
<li><hr class="dropdown-divider"></li>
<li><a class="dropdown-item" href="/Administration/MembershipApplications">Mitgliedsanträge</a></li>
<li><a class="dropdown-item" href="/Administration/DueSelections">Beitragseinstufungen</a></li>
```

- [ ] **Step 2: Add admin terms to Translations.md**

Append to `Translations.md`:

```markdown
## Administration

| German | English | Notes |
|---|---|---|
| Ausstehend | Pending | Application/due selection status |
| Genehmigt | Approved | |
| Abgelehnt | Rejected | |
| Automatisch genehmigt | Auto-Approved | Non-reduced dues |
| Anträge | Applications | |
| Einstufungen | Classifications / Selections | Due selections |
| Aktionen | Actions | |
| Eingereicht | Submitted | |
| Gewählter Beitrag | Selected Dues | |
| Geminderter Betrag | Reduced Amount | |
```

- [ ] **Step 3: Build full solution and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Summary of Changes

| Layer | Files Created | Files Modified |
|---|---|---|
| Data | — | MembershipApplication.cs, DueSelection.cs, M001 migration, MembershipApplicationRepository.cs, DueSelectionRepository.cs, ChapterRepository.cs |
| API | MembershipApplicationAdminDTO.cs, MembershipApplicationListRequest.cs, MembershipApplicationListResponse.cs, DueSelectionAdminDTO.cs, DueSelectionListRequest.cs, DueSelectionListResponse.cs | PermissionIdentifier.cs |
| Server | MembershipApplicationListEndpoint.cs, MembershipApplicationProcessEndpoint.cs, DueSelectionListEndpoint.cs, DueSelectionProcessEndpoint.cs | MembershipApplicationCreateEndpoint.cs |
| Blazor | MembershipApplicationAdmin.razor/.cs, DueSelectionAdmin.razor/.cs | MainLayout.razor |
| Docs | — | Translations.md, PermissionRepository.cs |
