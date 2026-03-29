# Motions & Voting System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a generic motion/voting system where chapter officers vote to approve/deny motions, with auto-resolution on majority. Membership applications and reduced due selections auto-spawn linked motions. Motions can be public or non-public, and track both approval status and realization status separately.

**Architecture:** New `Motion` and `MotionVote` entities. Motion has two status tracks: `ApprovalStatus` (Pending/Approved/Rejected/FormallyRejected/ClosedWithoutAction) and `IsRealized` flag. Each chapter officer can vote Approve/Deny/Abstain. When votes reach majority the motion auto-resolves, which cascades to linked MembershipApplication/DueSelection status. Officers are seeded as test data (Users + ChapterOfficer records). A public motion creation page and a Vorstandsarbeit motion list/detail/vote page are added.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB, Blazor WASM, Bootstrap 5, Bogus (test data)

---

## File Structure

### Data Model (new entities + migration updates)
- `Quartermaster.Data/Motions/Motion.cs` — Motion entity with enums
- `Quartermaster.Data/Motions/MotionVote.cs` — Vote entity with enum
- `Quartermaster.Data/Motions/MotionRepository.cs` — CRUD, list, vote logic, auto-resolution
- `Quartermaster.Data/ChapterAssociates/ChapterOfficerRepository.cs` — **create**, query officers for a chapter
- `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs` — add Motion + MotionVote tables
- `Quartermaster.Data/DbContext.cs` — register new tables/repositories

### API DTOs
- `Quartermaster.Api/Motions/MotionDTO.cs` — list item DTO
- `Quartermaster.Api/Motions/MotionDetailDTO.cs` — full detail with votes
- `Quartermaster.Api/Motions/MotionVoteDTO.cs` — individual vote record
- `Quartermaster.Api/Motions/MotionCreateRequest.cs` — public creation request
- `Quartermaster.Api/Motions/MotionListRequest.cs` — list query params
- `Quartermaster.Api/Motions/MotionListResponse.cs` — paginated response
- `Quartermaster.Api/Motions/MotionVoteRequest.cs` — cast vote request

### Server Endpoints
- `Quartermaster.Server/Motions/MotionListEndpoint.cs` — GET list (public motions for everyone, non-public for officers)
- `Quartermaster.Server/Motions/MotionDetailEndpoint.cs` — GET detail with votes
- `Quartermaster.Server/Motions/MotionCreateEndpoint.cs` — POST create (public)
- `Quartermaster.Server/Motions/MotionVoteEndpoint.cs` — POST vote
- `Quartermaster.Server/Motions/MotionStatusEndpoint.cs` — POST change status (formally reject / close without action / mark realized)
- `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs` — modify to spawn linked motion
- `Quartermaster.Server/TestData/TestDataSeeder.cs` — seed officers + test motions

### Blazor Pages
- `Quartermaster.Blazor/Pages/Motions/MotionCreate.razor` + `.cs` — public creation form
- `Quartermaster.Blazor/Pages/Administration/MotionList.razor` + `.cs` — officer list view
- `Quartermaster.Blazor/Pages/Administration/MotionDetail.razor` + `.cs` — detail + voting UI
- `Quartermaster.Blazor/Layout/MainLayout.razor` — add nav links

---

## Task 1: Motion & MotionVote Entities + Migration

**Files:**
- Create: `Quartermaster.Data/Motions/Motion.cs`
- Create: `Quartermaster.Data/Motions/MotionVote.cs`
- Modify: `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`
- Modify: `Quartermaster.Data/DbContext.cs`

- [ ] **Step 1: Create Motion entity**

Create `Quartermaster.Data/Motions/Motion.cs`:

```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Motions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Motion {
    public const string TableName = "Motions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChapterId { get; set; }

    // Author (may not be a registered user)
    public string AuthorName { get; set; } = "";
    public string AuthorEMail { get; set; } = "";

    // Content
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsPublic { get; set; } = true;

    // Link to source entity (optional)
    public Guid? LinkedMembershipApplicationId { get; set; }
    public Guid? LinkedDueSelectionId { get; set; }

    // Status
    public MotionApprovalStatus ApprovalStatus { get; set; }
    public bool IsRealized { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public enum MotionApprovalStatus {
    Pending,
    Approved,
    Rejected,
    FormallyRejected,
    ClosedWithoutAction
}
```

- [ ] **Step 2: Create MotionVote entity**

Create `Quartermaster.Data/Motions/MotionVote.cs`:

```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Motions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class MotionVote {
    public const string TableName = "MotionVotes";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MotionId { get; set; }
    public Guid UserId { get; set; }
    public VoteType Vote { get; set; }
    public DateTime VotedAt { get; set; }
}

public enum VoteType {
    Approve,
    Deny,
    Abstain
}
```

- [ ] **Step 3: Update M001 migration**

Add the Motions and MotionVotes tables to `M001_InitialStructureMigration.cs`. Add usings for `Quartermaster.Data.Motions`.

In `Up()`, add BEFORE the MembershipApplications table (since MembershipApplications will reference Motions later if needed, but for now just after ChapterAssociates):

```csharp
        Create.Table(Motion.TableName)
            .WithColumn(nameof(Motion.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(Motion.ChapterId)).AsGuid()
            .WithColumn(nameof(Motion.AuthorName)).AsString(256)
            .WithColumn(nameof(Motion.AuthorEMail)).AsString(256)
            .WithColumn(nameof(Motion.Title)).AsString(512)
            .WithColumn(nameof(Motion.Text)).AsString(8192)
            .WithColumn(nameof(Motion.IsPublic)).AsBoolean()
            .WithColumn(nameof(Motion.LinkedMembershipApplicationId)).AsGuid().Nullable()
            .WithColumn(nameof(Motion.LinkedDueSelectionId)).AsGuid().Nullable()
            .WithColumn(nameof(Motion.ApprovalStatus)).AsInt32()
            .WithColumn(nameof(Motion.IsRealized)).AsBoolean()
            .WithColumn(nameof(Motion.CreatedAt)).AsDateTime()
            .WithColumn(nameof(Motion.ResolvedAt)).AsDateTime().Nullable();

        Create.ForeignKey("FK_Motions_ChapterId_Chapters_Id")
            .FromTable(Motion.TableName).ForeignColumn(nameof(Motion.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Table(MotionVote.TableName)
            .WithColumn(nameof(MotionVote.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(MotionVote.MotionId)).AsGuid()
            .WithColumn(nameof(MotionVote.UserId)).AsGuid()
            .WithColumn(nameof(MotionVote.Vote)).AsInt32()
            .WithColumn(nameof(MotionVote.VotedAt)).AsDateTime();

        Create.ForeignKey("FK_MotionVotes_MotionId_Motions_Id")
            .FromTable(MotionVote.TableName).ForeignColumn(nameof(MotionVote.MotionId))
            .ToTable(Motion.TableName).PrimaryColumn(nameof(Motion.Id));

        Create.ForeignKey("FK_MotionVotes_UserId_Users_Id")
            .FromTable(MotionVote.TableName).ForeignColumn(nameof(MotionVote.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));
```

In `Down()`, add:

```csharp
        Delete.ForeignKey("FK_MotionVotes_MotionId_Motions_Id")
            .OnTable(MotionVote.TableName);
        Delete.ForeignKey("FK_MotionVotes_UserId_Users_Id")
            .OnTable(MotionVote.TableName);
        Delete.ForeignKey("FK_Motions_ChapterId_Chapters_Id")
            .OnTable(Motion.TableName);
```

And add to the table deletes:

```csharp
        Delete.Table(MotionVote.TableName);
        Delete.Table(Motion.TableName);
```

- [ ] **Step 4: Register in DbContext**

Add to `DbContext.cs`:
- `using Quartermaster.Data.Motions;`
- `using Quartermaster.Data.ChapterAssociates;`
- Properties: `public ITable<Motion> Motions => this.GetTable<Motion>();`
- `public ITable<MotionVote> MotionVotes => this.GetTable<MotionVote>();`
- `public ITable<ChapterOfficer> ChapterOfficers => this.GetTable<ChapterOfficer>();`
- In `AddRepositories`: `services.AddScoped<MotionRepository>();`
- In `AddRepositories`: `services.AddScoped<ChapterOfficerRepository>();`

- [ ] **Step 5: Build and verify**

Run: `cd /media/SMB/Quartermaster && dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 2: ChapterOfficerRepository + MotionRepository

**Files:**
- Create: `Quartermaster.Data/ChapterAssociates/ChapterOfficerRepository.cs`
- Create: `Quartermaster.Data/Motions/MotionRepository.cs`

- [ ] **Step 1: Create ChapterOfficerRepository**

```csharp
using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.ChapterAssociates;

public class ChapterOfficerRepository {
    private readonly DbContext _context;

    public ChapterOfficerRepository(DbContext context) {
        _context = context;
    }

    public List<ChapterOfficer> GetForChapter(Guid chapterId)
        => _context.ChapterOfficers.Where(o => o.ChapterId == chapterId).ToList();

    public int CountForChapter(Guid chapterId)
        => _context.ChapterOfficers.Where(o => o.ChapterId == chapterId).Count();

    public void Create(ChapterOfficer officer) => _context.Insert(officer);
}
```

- [ ] **Step 2: Create MotionRepository**

```csharp
using LinqToDB;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Motions;

public class MotionRepository {
    private readonly DbContext _context;

    public MotionRepository(DbContext context) {
        _context = context;
    }

    public Motion? Get(Guid id)
        => _context.Motions.Where(m => m.Id == id).FirstOrDefault();

    public void Create(Motion motion) => _context.Insert(motion);

    public (List<Motion> Items, int TotalCount) List(
        Guid? chapterId, MotionApprovalStatus? status, bool includeNonPublic, int page, int pageSize) {

        var q = _context.Motions.AsQueryable();

        if (chapterId.HasValue)
            q = q.Where(m => m.ChapterId == chapterId.Value);

        if (status != null)
            q = q.Where(m => m.ApprovalStatus == status.Value);

        if (!includeNonPublic)
            q = q.Where(m => m.IsPublic);

        var totalCount = q.Count();
        var items = q.OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public List<MotionVote> GetVotes(Guid motionId)
        => _context.MotionVotes.Where(v => v.MotionId == motionId).ToList();

    public MotionVote? GetVote(Guid motionId, Guid userId)
        => _context.MotionVotes
            .Where(v => v.MotionId == motionId && v.UserId == userId)
            .FirstOrDefault();

    public void CastVote(MotionVote vote) {
        var existing = GetVote(vote.MotionId, vote.UserId);
        if (existing != null) {
            _context.MotionVotes
                .Where(v => v.Id == existing.Id)
                .Set(v => v.Vote, vote.Vote)
                .Set(v => v.VotedAt, vote.VotedAt)
                .Update();
        } else {
            _context.Insert(vote);
        }
    }

    /// <summary>
    /// Check if majority is reached and auto-resolve the motion.
    /// Returns true if the motion was resolved by this call.
    /// </summary>
    public bool TryAutoResolve(Guid motionId, ChapterOfficerRepository officerRepo) {
        var motion = Get(motionId);
        if (motion == null || motion.ApprovalStatus != MotionApprovalStatus.Pending)
            return false;

        var officerCount = officerRepo.CountForChapter(motion.ChapterId);
        if (officerCount == 0)
            return false;

        var votes = GetVotes(motionId);
        var approveCount = votes.Count(v => v.Vote == VoteType.Approve);
        var denyCount = votes.Count(v => v.Vote == VoteType.Deny);
        var majority = (officerCount / 2) + 1;

        MotionApprovalStatus? newStatus = null;
        if (approveCount >= majority)
            newStatus = MotionApprovalStatus.Approved;
        else if (denyCount >= majority)
            newStatus = MotionApprovalStatus.Rejected;

        if (newStatus == null)
            return false;

        _context.Motions
            .Where(m => m.Id == motionId)
            .Set(m => m.ApprovalStatus, newStatus.Value)
            .Set(m => m.ResolvedAt, DateTime.UtcNow)
            .Update();

        // Cascade to linked entities
        if (motion.LinkedMembershipApplicationId.HasValue) {
            var appStatus = newStatus == MotionApprovalStatus.Approved
                ? ApplicationStatus.Approved
                : ApplicationStatus.Rejected;
            _context.MembershipApplications
                .Where(a => a.Id == motion.LinkedMembershipApplicationId.Value)
                .Set(a => a.Status, appStatus)
                .Set(a => a.ProcessedAt, DateTime.UtcNow)
                .Update();
        }

        if (motion.LinkedDueSelectionId.HasValue) {
            var dsStatus = newStatus == MotionApprovalStatus.Approved
                ? DueSelectionStatus.Approved
                : DueSelectionStatus.Rejected;
            _context.DueSelections
                .Where(d => d.Id == motion.LinkedDueSelectionId.Value)
                .Set(d => d.Status, dsStatus)
                .Set(d => d.ProcessedAt, DateTime.UtcNow)
                .Update();
        }

        return true;
    }

    public void UpdateApprovalStatus(Guid id, MotionApprovalStatus status) {
        _context.Motions
            .Where(m => m.Id == id)
            .Set(m => m.ApprovalStatus, status)
            .Set(m => m.ResolvedAt, DateTime.UtcNow)
            .Update();
    }

    public void SetRealized(Guid id, bool realized) {
        _context.Motions
            .Where(m => m.Id == id)
            .Set(m => m.IsRealized, realized)
            .Update();
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 3: API DTOs

**Files:**
- Create: `Quartermaster.Api/Motions/MotionDTO.cs`
- Create: `Quartermaster.Api/Motions/MotionDetailDTO.cs`
- Create: `Quartermaster.Api/Motions/MotionVoteDTO.cs`
- Create: `Quartermaster.Api/Motions/MotionCreateRequest.cs`
- Create: `Quartermaster.Api/Motions/MotionListRequest.cs`
- Create: `Quartermaster.Api/Motions/MotionListResponse.cs`
- Create: `Quartermaster.Api/Motions/MotionVoteRequest.cs`
- Create: `Quartermaster.Api/Motions/MotionStatusRequest.cs`

- [ ] **Step 1: Create all DTO files**

`MotionDTO.cs`:
```csharp
using System;

namespace Quartermaster.Api.Motions;

public class MotionDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsPublic { get; set; }
    public int ApprovalStatus { get; set; }
    public bool IsRealized { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
```

`MotionDetailDTO.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Motions;

public class MotionDetailDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorEMail { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsPublic { get; set; }
    public Guid? LinkedMembershipApplicationId { get; set; }
    public Guid? LinkedDueSelectionId { get; set; }
    public int ApprovalStatus { get; set; }
    public bool IsRealized { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public List<MotionVoteDTO> Votes { get; set; } = [];
    public int TotalOfficers { get; set; }
}
```

`MotionVoteDTO.cs`:
```csharp
using System;

namespace Quartermaster.Api.Motions;

public class MotionVoteDTO {
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string OfficerRole { get; set; } = "";
    public int Vote { get; set; }
    public DateTime VotedAt { get; set; }
}
```

`MotionCreateRequest.cs`:
```csharp
using System;

namespace Quartermaster.Api.Motions;

public class MotionCreateRequest {
    public Guid ChapterId { get; set; }
    public string AuthorName { get; set; } = "";
    public string AuthorEMail { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}
```

`MotionListRequest.cs`:
```csharp
using System;

namespace Quartermaster.Api.Motions;

public class MotionListRequest {
    public Guid? ChapterId { get; set; }
    public int? ApprovalStatus { get; set; }
    public bool IncludeNonPublic { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
```

`MotionListResponse.cs`:
```csharp
using System.Collections.Generic;

namespace Quartermaster.Api.Motions;

public class MotionListResponse {
    public List<MotionDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

`MotionVoteRequest.cs`:
```csharp
using System;

namespace Quartermaster.Api.Motions;

public class MotionVoteRequest {
    public Guid MotionId { get; set; }
    public Guid UserId { get; set; }
    public int Vote { get; set; } // 0=Approve, 1=Deny, 2=Abstain
}
```

`MotionStatusRequest.cs`:
```csharp
using System;

namespace Quartermaster.Api.Motions;

public class MotionStatusRequest {
    public Guid MotionId { get; set; }
    public int? ApprovalStatus { get; set; } // 3=FormallyRejected, 4=ClosedWithoutAction
    public bool? IsRealized { get; set; }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 4: Server Endpoints

**Files:**
- Create: `Quartermaster.Server/Motions/MotionListEndpoint.cs`
- Create: `Quartermaster.Server/Motions/MotionDetailEndpoint.cs`
- Create: `Quartermaster.Server/Motions/MotionCreateEndpoint.cs`
- Create: `Quartermaster.Server/Motions/MotionVoteEndpoint.cs`
- Create: `Quartermaster.Server/Motions/MotionStatusEndpoint.cs`

- [ ] **Step 1: Create all 5 endpoint files**

`MotionListEndpoint.cs`:
```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Motions;

public class MotionListEndpoint : Endpoint<MotionListRequest, MotionListResponse> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;

    public MotionListEndpoint(MotionRepository motionRepo, ChapterRepository chapterRepo) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/motions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionListRequest req, CancellationToken ct) {
        MotionApprovalStatus? status = req.ApprovalStatus.HasValue
            ? (MotionApprovalStatus)req.ApprovalStatus.Value
            : null;

        var (items, totalCount) = _motionRepo.List(
            req.ChapterId, status, req.IncludeNonPublic, req.Page, req.PageSize);

        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var dtos = items.Select(m => new MotionDTO {
            Id = m.Id,
            ChapterId = m.ChapterId,
            ChapterName = chapters.ContainsKey(m.ChapterId) ? chapters[m.ChapterId] : "",
            AuthorName = m.AuthorName,
            Title = m.Title,
            IsPublic = m.IsPublic,
            ApprovalStatus = (int)m.ApprovalStatus,
            IsRealized = m.IsRealized,
            CreatedAt = m.CreatedAt,
            ResolvedAt = m.ResolvedAt
        }).ToList();

        await SendAsync(new MotionListResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
```

`MotionDetailEndpoint.cs`:
```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Motions;

public class MotionDetailRequest {
    public Guid Id { get; set; }
}

public class MotionDetailEndpoint : Endpoint<MotionDetailRequest, MotionDetailDTO> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly UserRepository _userRepo;

    public MotionDetailEndpoint(MotionRepository motionRepo, ChapterRepository chapterRepo,
        ChapterOfficerRepository officerRepo, UserRepository userRepo) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _officerRepo = officerRepo;
        _userRepo = userRepo;
    }

    public override void Configure() {
        Get("/api/motions/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionDetailRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.Id);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var chapter = _chapterRepo.Get(motion.ChapterId);
        var officers = _officerRepo.GetForChapter(motion.ChapterId);
        var votes = _motionRepo.GetVotes(motion.Id);

        var voteDtos = votes.Select(v => {
            var user = _userRepo.GetById(v.UserId);
            var officer = officers.FirstOrDefault(o => o.UserId == v.UserId);
            return new MotionVoteDTO {
                UserId = v.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unbekannt",
                OfficerRole = officer != null ? officer.AssociateType.ToString() : "",
                Vote = (int)v.Vote,
                VotedAt = v.VotedAt
            };
        }).ToList();

        await SendAsync(new MotionDetailDTO {
            Id = motion.Id,
            ChapterId = motion.ChapterId,
            ChapterName = chapter?.Name ?? "",
            AuthorName = motion.AuthorName,
            AuthorEMail = motion.AuthorEMail,
            Title = motion.Title,
            Text = motion.Text,
            IsPublic = motion.IsPublic,
            LinkedMembershipApplicationId = motion.LinkedMembershipApplicationId,
            LinkedDueSelectionId = motion.LinkedDueSelectionId,
            ApprovalStatus = (int)motion.ApprovalStatus,
            IsRealized = motion.IsRealized,
            CreatedAt = motion.CreatedAt,
            ResolvedAt = motion.ResolvedAt,
            Votes = voteDtos,
            TotalOfficers = officers.Count
        }, cancellation: ct);
    }
}
```

`MotionCreateEndpoint.cs`:
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Motions;

public class MotionCreateEndpoint : Endpoint<MotionCreateRequest, MotionDTO> {
    private readonly MotionRepository _motionRepo;

    public MotionCreateEndpoint(MotionRepository motionRepo) {
        _motionRepo = motionRepo;
    }

    public override void Configure() {
        Post("/api/motions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionCreateRequest req, CancellationToken ct) {
        var motion = new Motion {
            ChapterId = req.ChapterId,
            AuthorName = req.AuthorName,
            AuthorEMail = req.AuthorEMail,
            Title = req.Title,
            Text = req.Text,
            IsPublic = true,
            ApprovalStatus = MotionApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _motionRepo.Create(motion);

        await SendAsync(new MotionDTO {
            Id = motion.Id,
            ChapterId = motion.ChapterId,
            AuthorName = motion.AuthorName,
            Title = motion.Title,
            IsPublic = true,
            ApprovalStatus = 0,
            CreatedAt = motion.CreatedAt
        }, cancellation: ct);
    }
}
```

`MotionVoteEndpoint.cs`:
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Motions;

public class MotionVoteEndpoint : Endpoint<MotionVoteRequest> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterOfficerRepository _officerRepo;

    public MotionVoteEndpoint(MotionRepository motionRepo, ChapterOfficerRepository officerRepo) {
        _motionRepo = motionRepo;
        _officerRepo = officerRepo;
    }

    public override void Configure() {
        Post("/api/motions/vote");
        AllowAnonymous(); // TODO: Replace with auth
    }

    public override async Task HandleAsync(MotionVoteRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.MotionId);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (motion.ApprovalStatus != MotionApprovalStatus.Pending) {
            await SendErrorsAsync(400, ct);
            return;
        }

        var vote = (VoteType)req.Vote;
        _motionRepo.CastVote(new MotionVote {
            MotionId = req.MotionId,
            UserId = req.UserId,
            Vote = vote,
            VotedAt = DateTime.UtcNow
        });

        _motionRepo.TryAutoResolve(req.MotionId, _officerRepo);

        await SendOkAsync(ct);
    }
}
```

`MotionStatusEndpoint.cs`:
```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Motions;

public class MotionStatusEndpoint : Endpoint<MotionStatusRequest> {
    private readonly MotionRepository _motionRepo;

    public MotionStatusEndpoint(MotionRepository motionRepo) {
        _motionRepo = motionRepo;
    }

    public override void Configure() {
        Post("/api/motions/status");
        AllowAnonymous(); // TODO: Replace with auth
    }

    public override async Task HandleAsync(MotionStatusRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.MotionId);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (req.ApprovalStatus.HasValue) {
            var status = (MotionApprovalStatus)req.ApprovalStatus.Value;
            if (status != MotionApprovalStatus.FormallyRejected && status != MotionApprovalStatus.ClosedWithoutAction) {
                await SendErrorsAsync(400, ct);
                return;
            }
            _motionRepo.UpdateApprovalStatus(req.MotionId, status);
        }

        if (req.IsRealized.HasValue)
            _motionRepo.SetRealized(req.MotionId, req.IsRealized.Value);

        await SendOkAsync(ct);
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 5: Auto-spawn Motions from Applications & Due Selections

**Files:**
- Modify: `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs`

- [ ] **Step 1: Spawn linked motion when creating a membership application**

Update `MembershipApplicationCreateEndpoint` to inject `MotionRepository` and create a linked motion. After creating the application, add:

```csharp
        // Spawn a linked motion for chapter approval
        if (application.ChapterId.HasValue) {
            var motion = new Motion {
                ChapterId = application.ChapterId.Value,
                AuthorName = "System",
                AuthorEMail = "",
                Title = $"Mitgliedsantrag: {application.FirstName} {application.LastName}",
                Text = $"<p>Mitgliedsantrag von <strong>{application.FirstName} {application.LastName}</strong></p>"
                    + $"<p>E-Mail: {application.EMail}</p>"
                    + $"<p>Adresse: {application.AddressStreet} {application.AddressHouseNbr}, {application.AddressPostCode} {application.AddressCity}</p>"
                    + $"<p><a href=\"/Administration/MembershipApplications/{application.Id}\">Antrag ansehen</a></p>",
                IsPublic = false,
                LinkedMembershipApplicationId = application.Id,
                ApprovalStatus = MotionApprovalStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _motionRepo.Create(motion);
        }
```

Also spawn a motion for reduced due selections. After creating dueSelection, if it's Pending (reduced):

```csharp
        if (dueSelectionId.HasValue && req.DueSelection != null
            && req.DueSelection.SelectedValuation == Api.DueSelector.SelectedValuation.Reduced
            && application.ChapterId.HasValue) {
            var dueMotion = new Motion {
                ChapterId = application.ChapterId.Value,
                AuthorName = "System",
                AuthorEMail = "",
                Title = $"Beitragsminderung: {application.FirstName} {application.LastName}",
                Text = $"<p>Antrag auf geminderten Beitrag von <strong>{application.FirstName} {application.LastName}</strong></p>"
                    + $"<p>Gewünschter Betrag: {req.DueSelection.ReducedAmount}€</p>"
                    + $"<p>Begründung: {req.DueSelection.ReducedJustification}</p>"
                    + $"<p><a href=\"/Administration/DueSelections/{dueSelectionId.Value}\">Einstufung ansehen</a></p>",
                IsPublic = false,
                LinkedDueSelectionId = dueSelectionId.Value,
                ApprovalStatus = MotionApprovalStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _motionRepo.Create(dueMotion);
        }
```

Add constructor injection for `MotionRepository _motionRepo` and the using `using Quartermaster.Data.Motions;`.

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 6: Seed Test Officers & Test Motions

**Files:**
- Modify: `Quartermaster.Server/TestData/TestDataSeeder.cs`

- [ ] **Step 1: Add officer and motion seeding**

Add `using Quartermaster.Data.ChapterAssociates;`, `using Quartermaster.Data.Motions;`, and `using Quartermaster.Data.Users;` to the seeder.

Add `UserRepository _userRepo` and `MotionRepository _motionRepo` and `ChapterOfficerRepository _officerRepo` to the constructor and `Seed` method parameters (or inject via constructor).

At the start of `Seed()`, create test users and assign them as officers to each state chapter:

```csharp
        // Create test officer users (3 per state chapter)
        var officerTypes = new[] { ChapterOfficerType.Captain, ChapterOfficerType.FirstOfficer, ChapterOfficerType.Quartermaster };
        var officerUsers = new List<User>();

        foreach (var chapter in stateChapters) {
            for (var j = 0; j < 3; j++) {
                var user = new User {
                    Id = Guid.NewGuid(),
                    Username = faker.Internet.UserName(),
                    FirstName = faker.Name.FirstName(),
                    LastName = faker.Name.LastName(),
                    EMail = faker.Internet.Email(),
                    ChapterId = chapter.Id,
                    MemberSince = faker.Date.Past(5)
                };
                _context.Insert(user);
                officerUsers.Add(user);

                _context.Insert(new ChapterOfficer {
                    UserId = user.Id,
                    ChapterId = chapter.Id,
                    AssociateType = officerTypes[j]
                });
            }
        }
```

Then create test motions (some public, some with votes):

```csharp
        // Create test motions
        var motionTitles = new[] {
            "Antrag auf Änderung der Geschäftsordnung",
            "Antrag auf Durchführung eines Stammtisches",
            "Antrag auf Finanzierung von Wahlkampfmaterial",
            "Antrag auf Erstellung einer neuen Webseite",
            "Antrag auf Teilnahme am Stadtfest"
        };

        for (var i = 0; i < 15; i++) {
            var chapter = faker.PickRandom(stateChapters);
            var chapterOfficers = officerUsers.Where(u => u.ChapterId == chapter.Id).ToList();

            var motion = new Motion {
                Id = Guid.NewGuid(),
                ChapterId = chapter.Id,
                AuthorName = faker.Name.FullName(),
                AuthorEMail = faker.Internet.Email(),
                Title = faker.PickRandom(motionTitles),
                Text = $"<p>{faker.Lorem.Paragraphs(2)}</p>",
                IsPublic = faker.Random.Bool(0.7f),
                ApprovalStatus = MotionApprovalStatus.Pending,
                CreatedAt = faker.Date.Between(DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow)
            };
            _context.Insert(motion);

            // Some motions get votes
            if (faker.Random.Bool(0.6f)) {
                foreach (var officer in chapterOfficers) {
                    if (faker.Random.Bool(0.8f)) {
                        _context.Insert(new MotionVote {
                            Id = Guid.NewGuid(),
                            MotionId = motion.Id,
                            UserId = officer.Id,
                            Vote = faker.PickRandom<VoteType>(),
                            VotedAt = faker.Date.Between(motion.CreatedAt, DateTime.UtcNow)
                        });
                    }
                }
            }
        }
```

Update the constructor to accept the additional repositories and pass them through.

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 7: Blazor — Motion Creation Page (Public)

**Files:**
- Create: `Quartermaster.Blazor/Pages/Motions/MotionCreate.razor`
- Create: `Quartermaster.Blazor/Pages/Motions/MotionCreate.razor.cs`

- [ ] **Step 1: Create MotionCreate.razor**

```razor
@page "/Motions/Create"
@using Quartermaster.Api.Motions
@using Quartermaster.Api.Chapters

<div class="mb-3">
    <h3>Antrag einreichen</h3>
</div>

<div class="mb-3">
    <label class="form-label">Gliederung <RequiredStar /></label>
    <select class="form-select" @bind="SelectedChapterId">
        <option value="">- Bitte auswählen -</option>
        @if (Chapters != null) {
            @foreach (var chapter in Chapters) {
                <option value="@chapter.Id">@chapter.Name</option>
            }
        }
    </select>
</div>

<div class="mb-3">
    <label class="form-label">Dein Name <RequiredStar /></label>
    <input type="text" class="form-control" @bind="AuthorName" @bind:event="oninput" />
</div>

<div class="mb-3">
    <label class="form-label">Deine E-Mail Adresse <RequiredStar /></label>
    <input type="email" class="form-control" @bind="AuthorEMail" @bind:event="oninput" />
</div>

<div class="mb-3">
    <label class="form-label">Titel des Antrags <RequiredStar /></label>
    <input type="text" class="form-control" @bind="Title" @bind:event="oninput" />
</div>

<div class="mb-3">
    <label class="form-label">Antragstext <RequiredStar /></label>
    <textarea class="form-control" rows="6" @bind="Text" @bind:event="oninput" maxlength="8192"></textarea>
</div>

<div class="d-flex justify-content-end">
    <button class="btn btn-primary @(CanSubmit() ? "" : "disabled")" @onclick="Submit">
        Antrag einreichen
    </button>
</div>
```

- [ ] **Step 2: Create MotionCreate.razor.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Motions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Motions;

public partial class MotionCreate {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<ChapterDTO>? Chapters;
    private string SelectedChapterId { get; set; } = "";
    private string AuthorName { get; set; } = "";
    private string AuthorEMail { get; set; } = "";
    private string Title { get; set; } = "";
    private string Text { get; set; } = "";

    protected override async Task OnInitializedAsync() {
        Chapters = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");
    }

    private bool CanSubmit() {
        if (string.IsNullOrEmpty(SelectedChapterId))
            return false;
        if (string.IsNullOrEmpty(AuthorName))
            return false;
        if (string.IsNullOrEmpty(AuthorEMail))
            return false;
        if (string.IsNullOrEmpty(Title))
            return false;
        if (string.IsNullOrEmpty(Text))
            return false;
        return true;
    }

    private async Task Submit() {
        if (!Guid.TryParse(SelectedChapterId, out var chapterId))
            return;

        var result = await Http.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapterId,
            AuthorName = AuthorName,
            AuthorEMail = AuthorEMail,
            Title = Title,
            Text = Text
        });

        if (result.IsSuccessStatusCode) {
            NavigationManager.NavigateTo("/");
            ToastService.Toast("Dein Antrag wurde eingereicht!", "success");
        } else {
            ToastService.Toast("Es ist ein Fehler aufgetreten.", "danger");
        }
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 8: Blazor — Motion List Page (Vorstandsarbeit)

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/MotionList.razor`
- Create: `Quartermaster.Blazor/Pages/Administration/MotionList.razor.cs`

- [ ] **Step 1: Create MotionList.razor**

```razor
@page "/Administration/Motions"
@using Quartermaster.Api.Motions
@using Quartermaster.Api.Chapters

<div class="mb-3">
    <h3>Vorstandsarbeit - Anträge</h3>
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
            <option value="3">Formal abgelehnt</option>
            <option value="4">Ohne Beschluss geschlossen</option>
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
                <th>Titel</th>
                <th>Antragsteller</th>
                <th>Gliederung</th>
                <th>Sichtbarkeit</th>
                <th>Status</th>
                <th>Umgesetzt</th>
                <th>Erstellt</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var motion in Response.Items) {
                <tr>
                    <td><a href="/Administration/Motions/@motion.Id">@motion.Title</a></td>
                    <td>@motion.AuthorName</td>
                    <td>@motion.ChapterName</td>
                    <td>
                        @if (motion.IsPublic) {
                            <span class="badge border border-info text-info-emphasis">Öffentlich</span>
                        } else {
                            <span class="badge border border-secondary text-secondary-emphasis">Nicht öffentlich</span>
                        }
                    </td>
                    <td>@ApprovalLabel(motion.ApprovalStatus)</td>
                    <td>
                        @if (motion.IsRealized) {
                            <i class="bi bi-check-circle-fill text-success"></i>
                        }
                    </td>
                    <td>@motion.CreatedAt.ToString("dd.MM.yyyy")</td>
                </tr>
            }
        </tbody>
    </table>

    <Pagination CurrentPage="CurrentPage" TotalPages="TotalPages" OnPageChanged="GoToPage" />
}
```

- [ ] **Step 2: Create MotionList.razor.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Motions;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MotionList {
    [Inject]
    public required HttpClient Http { get; set; }

    private List<ChapterDTO>? Chapters;
    private MotionListResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private Guid? SelectedChapterId;
    private int? SelectedStatus = 0;

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

    private async Task GoToPage(int selectedPage) {
        CurrentPage = selectedPage;
        await Search();
    }

    private async Task Search() {
        Loading = true;
        StateHasChanged();

        var url = $"/api/motions?page={CurrentPage}&pageSize={PageSize}&includeNonPublic=true";
        if (SelectedChapterId.HasValue)
            url += $"&chapterId={SelectedChapterId.Value}";
        if (SelectedStatus.HasValue)
            url += $"&approvalStatus={SelectedStatus.Value}";

        Response = await Http.GetFromJsonAsync<MotionListResponse>(url);

        Loading = false;
        StateHasChanged();
    }

    private static string ApprovalLabel(int status) => status switch {
        0 => "Ausstehend",
        1 => "Genehmigt",
        2 => "Abgelehnt",
        3 => "Formal abgelehnt",
        4 => "Ohne Beschluss",
        _ => "Unbekannt"
    };
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 9: Blazor — Motion Detail + Voting Page

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/MotionDetail.razor`
- Create: `Quartermaster.Blazor/Pages/Administration/MotionDetail.razor.cs`

- [ ] **Step 1: Create MotionDetail.razor**

```razor
@page "/Administration/Motions/{Id:guid}"
@using Quartermaster.Api.Motions

<div class="mb-3">
    <a href="/Administration/Motions" class="btn btn-sm btn-outline-secondary">
        <i class="bi bi-arrow-left"></i> Zurück zur Übersicht
    </a>
</div>

@if (Loading) {
    <div class="d-flex justify-content-center my-4">
        <div class="spinner-border" role="status"></div>
    </div>
} else if (Motion == null) {
    <div class="alert alert-danger">Antrag nicht gefunden.</div>
} else {
    <div class="d-flex justify-content-between align-items-center mb-3">
        <h3>@Motion.Title</h3>
        <div class="d-flex gap-2">
            @if (Motion.IsRealized) {
                <span class="badge border border-success text-success-emphasis fs-6">Umgesetzt</span>
            }
            <span class="badge border fs-6 @ApprovalBadgeClass(Motion.ApprovalStatus)">
                @ApprovalLabel(Motion.ApprovalStatus)
            </span>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>Details</h5>
            <table class="table table-borderless mb-0">
                <tbody>
                    <tr><th style="width:200px">Antragsteller</th><td>@Motion.AuthorName</td></tr>
                    <tr><th>E-Mail</th><td>@Motion.AuthorEMail</td></tr>
                    <tr><th>Gliederung</th><td>@Motion.ChapterName</td></tr>
                    <tr>
                        <th>Sichtbarkeit</th>
                        <td>@(Motion.IsPublic ? "Öffentlich" : "Nicht öffentlich (enthält personenbezogene Daten)")</td>
                    </tr>
                    <tr><th>Erstellt</th><td>@Motion.CreatedAt.ToString("dd.MM.yyyy HH:mm")</td></tr>
                    @if (Motion.ResolvedAt != null) {
                        <tr><th>Beschlossen</th><td>@Motion.ResolvedAt.Value.ToString("dd.MM.yyyy HH:mm")</td></tr>
                    }
                    @if (Motion.LinkedMembershipApplicationId != null) {
                        <tr>
                            <th>Verknüpfung</th>
                            <td><a href="/Administration/MembershipApplications/@Motion.LinkedMembershipApplicationId">Mitgliedsantrag ansehen</a></td>
                        </tr>
                    }
                    @if (Motion.LinkedDueSelectionId != null) {
                        <tr>
                            <th>Verknüpfung</th>
                            <td><a href="/Administration/DueSelections/@Motion.LinkedDueSelectionId">Beitragseinstufung ansehen</a></td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>Antragstext</h5>
            <div>@((MarkupString)Motion.Text)</div>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>Abstimmung (@Motion.Votes.Count / @Motion.TotalOfficers Stimmen)</h5>

            @if (Motion.Votes.Count > 0) {
                <table class="table table-sm mb-3">
                    <thead>
                        <tr>
                            <th>Name</th>
                            <th>Rolle</th>
                            <th>Stimme</th>
                            <th>Datum</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var vote in Motion.Votes) {
                            <tr>
                                <td>@vote.UserName</td>
                                <td>@OfficerRoleLabel(vote.OfficerRole)</td>
                                <td>
                                    @if (vote.Vote == 0) {
                                        <span class="text-success"><i class="bi bi-hand-thumbs-up-fill"></i> Dafür</span>
                                    } else if (vote.Vote == 1) {
                                        <span class="text-danger"><i class="bi bi-hand-thumbs-down-fill"></i> Dagegen</span>
                                    } else {
                                        <span class="text-secondary"><i class="bi bi-dash-circle"></i> Enthaltung</span>
                                    }
                                </td>
                                <td>@vote.VotedAt.ToString("dd.MM.yyyy HH:mm")</td>
                            </tr>
                        }
                    </tbody>
                </table>
            } else {
                <p class="text-secondary">Noch keine Stimmen abgegeben.</p>
            }

            @if (Motion.ApprovalStatus == 0 && Officers != null && Officers.Count > 0) {
                <hr />
                <h6>Stimme abgeben</h6>
                <div class="d-flex gap-2 align-items-center">
                    <select class="form-select" style="width: auto;" @bind="SelectedVoterId">
                        <option value="">— Vorstandsmitglied —</option>
                        @foreach (var officer in Officers) {
                            <option value="@officer.UserId">@officer.UserName (@OfficerRoleLabel(officer.OfficerRole))</option>
                        }
                    </select>
                    <button class="btn btn-success btn-sm" @onclick="() => CastVote(0)">
                        <i class="bi bi-hand-thumbs-up-fill"></i> Dafür
                    </button>
                    <button class="btn btn-danger btn-sm" @onclick="() => CastVote(1)">
                        <i class="bi bi-hand-thumbs-down-fill"></i> Dagegen
                    </button>
                    <button class="btn btn-secondary btn-sm" @onclick="() => CastVote(2)">
                        <i class="bi bi-dash-circle"></i> Enthaltung
                    </button>
                </div>
            }
        </div>
    </div>

    @if (Motion.ApprovalStatus == 0) {
        <div class="card mb-3">
            <div class="card-body">
                <h5>Verwaltung</h5>
                <div class="d-flex gap-2">
                    <button class="btn btn-outline-warning btn-sm" @onclick="() => SetStatus(3)">
                        Formal ablehnen
                    </button>
                    <button class="btn btn-outline-secondary btn-sm" @onclick="() => SetStatus(4)">
                        Ohne Beschluss schließen
                    </button>
                </div>
            </div>
        </div>
    }

    @if (Motion.ApprovalStatus == 1 && !Motion.IsRealized) {
        <div class="d-flex">
            <button class="btn btn-success" @onclick="MarkRealized">
                <i class="bi bi-check-circle"></i> Als umgesetzt markieren
            </button>
        </div>
    }
}
```

- [ ] **Step 2: Create MotionDetail.razor.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Motions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MotionDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MotionDetailDTO? Motion;
    private List<MotionVoteDTO>? Officers;
    private bool Loading = true;
    private string SelectedVoterId { get; set; } = "";

    protected override async Task OnInitializedAsync() {
        await LoadMotion();
    }

    private async Task LoadMotion() {
        Loading = true;
        try {
            Motion = await Http.GetFromJsonAsync<MotionDetailDTO>($"/api/motions/{Id}");
            if (Motion != null) {
                // Build officer list for voting dropdown (all officers, not just those who voted)
                Officers = Motion.Votes.ToList();
            }
        } catch (HttpRequestException) { }
        Loading = false;
    }

    private async Task CastVote(int vote) {
        if (!Guid.TryParse(SelectedVoterId, out var userId))
            return;

        await Http.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = Id,
            UserId = userId,
            Vote = vote
        });

        await LoadMotion();
        StateHasChanged();
    }

    private async Task SetStatus(int status) {
        await Http.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = Id,
            ApprovalStatus = status
        });

        ToastService.Toast("Status aktualisiert.", "success");
        await LoadMotion();
        StateHasChanged();
    }

    private async Task MarkRealized() {
        await Http.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = Id,
            IsRealized = true
        });

        ToastService.Toast("Als umgesetzt markiert.", "success");
        await LoadMotion();
        StateHasChanged();
    }

    private static string ApprovalLabel(int status) => status switch {
        0 => "Ausstehend",
        1 => "Genehmigt",
        2 => "Abgelehnt",
        3 => "Formal abgelehnt",
        4 => "Ohne Beschluss",
        _ => "Unbekannt"
    };

    private static string ApprovalBadgeClass(int status) => status switch {
        0 => "border-warning text-warning-emphasis",
        1 => "border-success text-success-emphasis",
        2 => "border-danger text-danger-emphasis",
        3 => "border-secondary text-secondary-emphasis",
        4 => "border-secondary text-secondary-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };

    private static string OfficerRoleLabel(string role) => role switch {
        "Captain" => "Vorsitzender",
        "FirstOfficer" => "Stellv. Vorsitzender",
        "Quartermaster" => "Schatzmeister",
        "Treasurer" => "Kassierer",
        "ViceTreasurer" => "Stellv. Kassierer",
        "PoliticalDirector" => "Pol. Geschäftsführer",
        "Member" => "Beisitzer",
        _ => role
    };
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Task 10: Navigation + Translations

**Files:**
- Modify: `Quartermaster.Blazor/Layout/MainLayout.razor`
- Modify: `Quartermaster.Documentation/Translations.md`

- [ ] **Step 1: Update navigation**

In `MainLayout.razor`, add to Vorstandsarbeit dropdown:
```razor
                            <li><a class="dropdown-item" href="/Administration/Motions">Anträge</a></li>
```

Add to Mitgliedsportal dropdown:
```razor
                            <li><a class="dropdown-item" href="/Motions/Create">Antrag einreichen</a></li>
```

- [ ] **Step 2: Add motion terms to Translations.md**

Append:
```markdown

## Motions (Anträge)

| German | English | Notes |
|---|---|---|
| Antrag | Motion | A formal proposal for chapter voting |
| Anträge | Motions | |
| Antrag einreichen | Submit Motion | |
| Antragsteller | Author / Proposer | |
| Antragstext | Motion Text | |
| Abstimmung | Vote / Voting | |
| Stimme | Vote (noun) | |
| Dafür | Approve / In Favor | |
| Dagegen | Deny / Against | |
| Enthaltung | Abstain | |
| Formal abgelehnt | Formally Rejected | Doesn't meet requirements |
| Ohne Beschluss geschlossen | Closed Without Action | |
| Umgesetzt | Realized / Implemented | |
| Öffentlich | Public | |
| Nicht öffentlich | Non-public | Contains personal data |
| Vorstandsmitglied | Board Member / Officer | |
| Vorsitzender | Chair | Captain |
| Stellv. Vorsitzender | Vice Chair | FirstOfficer |
| Schatzmeister | Treasurer | Quartermaster (pirate naming) |
```

- [ ] **Step 3: Build full solution and verify**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

---

## Summary

| Layer | Files Created | Files Modified |
|---|---|---|
| Data | Motion.cs, MotionVote.cs, MotionRepository.cs, ChapterOfficerRepository.cs | M001 migration, DbContext.cs |
| API | MotionDTO.cs, MotionDetailDTO.cs, MotionVoteDTO.cs, MotionCreateRequest.cs, MotionListRequest.cs, MotionListResponse.cs, MotionVoteRequest.cs, MotionStatusRequest.cs | — |
| Server | MotionListEndpoint.cs, MotionDetailEndpoint.cs, MotionCreateEndpoint.cs, MotionVoteEndpoint.cs, MotionStatusEndpoint.cs | MembershipApplicationCreateEndpoint.cs, TestDataSeeder.cs |
| Blazor | MotionCreate.razor/.cs, MotionList.razor/.cs, MotionDetail.razor/.cs | MainLayout.razor |
| Docs | — | Translations.md |
