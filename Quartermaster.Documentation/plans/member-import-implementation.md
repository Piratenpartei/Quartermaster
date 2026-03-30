# Member Import System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import member data from a daily CSV export, resolve chapter assignments via ExternalCode hierarchy, and provide admin UI for viewing members and import history.

**Architecture:** New `Member` and `MemberImportLog` entities in Data layer. `ExternalCode` added to `Chapter` for CSV-to-chapter mapping. CsvHelper parses the semicolon-delimited export. A `BackgroundService` polls the configured file path, hashes for change detection, and upserts members. FastEndpoints expose list/detail/import/history APIs. Blazor pages provide admin UI.

**Tech Stack:** CsvHelper (MIT), ASP.NET Core BackgroundService, LinqToDB, FastEndpoints, Blazor WASM, existing options system.

---

## File Structure

### Data Layer (`Quartermaster.Data/`)
| File | Responsibility |
|------|---------------|
| `Members/Member.cs` | **Create** — Member entity with all imported fields + resolved FKs |
| `Members/MemberImportLog.cs` | **Create** — Import log entity (stats, errors, hash) |
| `Members/MemberRepository.cs` | **Create** — CRUD, search, upsert by member number |
| `Chapters/Chapter.cs` | **Modify** — Add `ExternalCode` property |
| `Chapters/ChapterRepository.cs` | **Modify** — Add `FindByExternalCode()`, extend `SupplementDefaults()` with Bezirk/Kreis chapters |
| `Migrations/M001_InitialStructureMigration.cs` | **Modify** — Add Members table, MemberImportLogs table, ExternalCode column on Chapters |
| `DbContext.cs` | **Modify** — Add `Members` and `MemberImportLogs` ITable, register `MemberRepository` |

### API Layer (`Quartermaster.Api/`)
| File | Responsibility |
|------|---------------|
| `Members/MemberDTO.cs` | **Create** — List view DTO |
| `Members/MemberDetailDTO.cs` | **Create** — Full detail DTO |
| `Members/MemberSearchRequest.cs` | **Create** — Search/filter request |
| `Members/MemberSearchResponse.cs` | **Create** — Paginated response |
| `Members/MemberImportLogDTO.cs` | **Create** — Import log DTO |
| `Members/MemberImportLogListResponse.cs` | **Create** — Paginated import history response |

### Server Layer (`Quartermaster.Server/`)
| File | Responsibility |
|------|---------------|
| `Members/MemberCsvRecord.cs` | **Create** — CsvHelper mapping class |
| `Members/MemberImportService.cs` | **Create** — Core import logic (parse, resolve chapters, upsert) |
| `Members/MemberImportHostedService.cs` | **Create** — BackgroundService polling timer |
| `Members/MemberListEndpoint.cs` | **Create** — GET /api/members |
| `Members/MemberDetailEndpoint.cs` | **Create** — GET /api/members/{id} |
| `Members/MemberImportTriggerEndpoint.cs` | **Create** — POST /api/members/import |
| `Members/MemberImportHistoryEndpoint.cs` | **Create** — GET /api/members/import/history |

### Blazor Layer (`Quartermaster.Blazor/`)
| File | Responsibility |
|------|---------------|
| `Pages/Administration/MemberList.razor` | **Create** — Member list page |
| `Pages/Administration/MemberList.razor.cs` | **Create** — Member list code-behind |
| `Pages/Administration/MemberDetail.razor` | **Create** — Member detail page |
| `Pages/Administration/MemberDetail.razor.cs` | **Create** — Member detail code-behind |
| `Pages/Administration/MemberImportHistory.razor` | **Create** — Import history page |
| `Pages/Administration/MemberImportHistory.razor.cs` | **Create** — Import history code-behind |
| `Layout/MainLayout.razor` | **Modify** — Add "Mitglieder" nav link |

---

## Task 1: Member and MemberImportLog Entities + Migration

**Files:**
- Create: `Quartermaster.Data/Members/Member.cs`
- Create: `Quartermaster.Data/Members/MemberImportLog.cs`
- Modify: `Quartermaster.Data/Chapters/Chapter.cs`
- Modify: `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`
- Modify: `Quartermaster.Data/DbContext.cs`

- [ ] **Step 1: Create the Member entity**

Create `Quartermaster.Data/Members/Member.cs`:

```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Members;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Member {
    public const string TableName = "Members";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public int MemberNumber { get; set; }
    public string? AdmissionReference { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Street { get; set; }
    public string? Country { get; set; }
    public string? PostCode { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? EMail { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Citizenship { get; set; }
    public decimal MembershipFee { get; set; }
    public decimal ReducedFee { get; set; }
    public decimal? FirstFee { get; set; }
    public decimal? OpenFeeTotal { get; set; }
    public DateTime? ReducedFeeEnd { get; set; }
    public DateTime? EntryDate { get; set; }
    public DateTime? ExitDate { get; set; }
    public string? FederalState { get; set; }
    public string? County { get; set; }
    public string? Municipality { get; set; }
    public bool IsPending { get; set; }
    public bool HasVotingRights { get; set; }
    public bool ReceivesSurveys { get; set; }
    public bool ReceivesActions { get; set; }
    public bool ReceivesNewsletter { get; set; }
    public bool PostBounce { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? ResidenceAdministrativeDivisionId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime LastImportedAt { get; set; }
}
```

- [ ] **Step 2: Create the MemberImportLog entity**

Create `Quartermaster.Data/Members/MemberImportLog.cs`:

```csharp
using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Members;

[Table(TableName, IsColumnAttributeRequired = false)]
public class MemberImportLog {
    public const string TableName = "MemberImportLogs";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ImportedAt { get; set; }
    public string FileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public int TotalRecords { get; set; }
    public int NewRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int ErrorCount { get; set; }
    public string? Errors { get; set; }
    public long DurationMs { get; set; }
}
```

- [ ] **Step 3: Add ExternalCode to Chapter entity**

In `Quartermaster.Data/Chapters/Chapter.cs`, add below the `ShortCode` property:

```csharp
public string? ExternalCode { get; set; }
```

- [ ] **Step 4: Add Members and MemberImportLogs tables to migration**

In `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`, add the following in the `Up()` method, after the `MembershipApplications` foreign keys block (before the closing `}` of `Up()`):

```csharp
Create.Table(Members.Member.TableName)
    .WithColumn(nameof(Members.Member.Id)).AsGuid().PrimaryKey().Indexed()
    .WithColumn(nameof(Members.Member.MemberNumber)).AsInt32().Unique()
    .WithColumn(nameof(Members.Member.AdmissionReference)).AsString(64).Nullable()
    .WithColumn(nameof(Members.Member.FirstName)).AsString(256)
    .WithColumn(nameof(Members.Member.LastName)).AsString(256)
    .WithColumn(nameof(Members.Member.Street)).AsString(256).Nullable()
    .WithColumn(nameof(Members.Member.Country)).AsString(16).Nullable()
    .WithColumn(nameof(Members.Member.PostCode)).AsString(16).Nullable()
    .WithColumn(nameof(Members.Member.City)).AsString(256).Nullable()
    .WithColumn(nameof(Members.Member.Phone)).AsString(64).Nullable()
    .WithColumn(nameof(Members.Member.EMail)).AsString(256).Nullable()
    .WithColumn(nameof(Members.Member.DateOfBirth)).AsDateTime().Nullable()
    .WithColumn(nameof(Members.Member.Citizenship)).AsString(64).Nullable()
    .WithColumn(nameof(Members.Member.MembershipFee)).AsDecimal()
    .WithColumn(nameof(Members.Member.ReducedFee)).AsDecimal()
    .WithColumn(nameof(Members.Member.FirstFee)).AsDecimal().Nullable()
    .WithColumn(nameof(Members.Member.OpenFeeTotal)).AsDecimal().Nullable()
    .WithColumn(nameof(Members.Member.ReducedFeeEnd)).AsDateTime().Nullable()
    .WithColumn(nameof(Members.Member.EntryDate)).AsDateTime().Nullable()
    .WithColumn(nameof(Members.Member.ExitDate)).AsDateTime().Nullable()
    .WithColumn(nameof(Members.Member.FederalState)).AsString(16).Nullable()
    .WithColumn(nameof(Members.Member.County)).AsString(256).Nullable()
    .WithColumn(nameof(Members.Member.Municipality)).AsString(256).Nullable()
    .WithColumn(nameof(Members.Member.IsPending)).AsBoolean()
    .WithColumn(nameof(Members.Member.HasVotingRights)).AsBoolean()
    .WithColumn(nameof(Members.Member.ReceivesSurveys)).AsBoolean()
    .WithColumn(nameof(Members.Member.ReceivesActions)).AsBoolean()
    .WithColumn(nameof(Members.Member.ReceivesNewsletter)).AsBoolean()
    .WithColumn(nameof(Members.Member.PostBounce)).AsBoolean()
    .WithColumn(nameof(Members.Member.ChapterId)).AsGuid().Nullable()
    .WithColumn(nameof(Members.Member.ResidenceAdministrativeDivisionId)).AsGuid().Nullable()
    .WithColumn(nameof(Members.Member.UserId)).AsGuid().Nullable()
    .WithColumn(nameof(Members.Member.LastImportedAt)).AsDateTime();

Create.ForeignKey("FK_Members_ChapterId_Chapters_Id")
    .FromTable(Members.Member.TableName).ForeignColumn(nameof(Members.Member.ChapterId))
    .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

Create.ForeignKey("FK_Members_ResAdminDivId_AdminDivs_Id")
    .FromTable(Members.Member.TableName).ForeignColumn(nameof(Members.Member.ResidenceAdministrativeDivisionId))
    .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

Create.ForeignKey("FK_Members_UserId_Users_Id")
    .FromTable(Members.Member.TableName).ForeignColumn(nameof(Members.Member.UserId))
    .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

Create.Table(Members.MemberImportLog.TableName)
    .WithColumn(nameof(Members.MemberImportLog.Id)).AsGuid().PrimaryKey()
    .WithColumn(nameof(Members.MemberImportLog.ImportedAt)).AsDateTime()
    .WithColumn(nameof(Members.MemberImportLog.FileName)).AsString(512)
    .WithColumn(nameof(Members.MemberImportLog.FileHash)).AsString(128)
    .WithColumn(nameof(Members.MemberImportLog.TotalRecords)).AsInt32()
    .WithColumn(nameof(Members.MemberImportLog.NewRecords)).AsInt32()
    .WithColumn(nameof(Members.MemberImportLog.UpdatedRecords)).AsInt32()
    .WithColumn(nameof(Members.MemberImportLog.ErrorCount)).AsInt32()
    .WithColumn(nameof(Members.MemberImportLog.Errors)).AsString(8192).Nullable()
    .WithColumn(nameof(Members.MemberImportLog.DurationMs)).AsInt64();
```

Add the `ExternalCode` column to the Chapters table definition (after the `ShortCode` line):

```csharp
.WithColumn(nameof(Chapter.ExternalCode)).AsString(128).Nullable();
```

Add the `using Quartermaster.Data.Members;` to the migration file imports.

In the `Down()` method, add cleanup before the existing deletes (near the top):

```csharp
Delete.ForeignKey("FK_Members_ChapterId_Chapters_Id")
    .OnTable(Members.Member.TableName);
Delete.ForeignKey("FK_Members_ResAdminDivId_AdminDivs_Id")
    .OnTable(Members.Member.TableName);
Delete.ForeignKey("FK_Members_UserId_Users_Id")
    .OnTable(Members.Member.TableName);
Delete.Table(Members.MemberImportLog.TableName);
Delete.Table(Members.Member.TableName);
```

- [ ] **Step 5: Update DbContext**

In `Quartermaster.Data/DbContext.cs`:

Add the using at the top:
```csharp
using Quartermaster.Data.Members;
```

Add ITable properties (after the existing `OptionDefinitions` line):
```csharp
public ITable<Member> Members => this.GetTable<Member>();
public ITable<MemberImportLog> MemberImportLogs => this.GetTable<MemberImportLog>();
```

Add repository registration in `AddRepositories()`:
```csharp
services.AddScoped<MemberRepository>();
```

- [ ] **Step 6: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Data/Quartermaster.Data.csproj`

This will fail because `MemberRepository` doesn't exist yet — that's expected and will be created in the next task.

- [ ] **Step 7: Commit**

```bash
git add Quartermaster.Data/Members/Member.cs Quartermaster.Data/Members/MemberImportLog.cs Quartermaster.Data/Chapters/Chapter.cs Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs Quartermaster.Data/DbContext.cs
git commit -m "feat: add Member and MemberImportLog entities, ExternalCode on Chapter, migration"
```

---

## Task 2: MemberRepository

**Files:**
- Create: `Quartermaster.Data/Members/MemberRepository.cs`

- [ ] **Step 1: Create the MemberRepository**

Create `Quartermaster.Data/Members/MemberRepository.cs`:

```csharp
using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Members;

public class MemberRepository {
    private readonly DbContext _context;

    public MemberRepository(DbContext context) {
        _context = context;
    }

    public Member? Get(Guid id)
        => _context.Members.Where(m => m.Id == id).FirstOrDefault();

    public Member? GetByMemberNumber(int memberNumber)
        => _context.Members.Where(m => m.MemberNumber == memberNumber).FirstOrDefault();

    public (List<Member> Items, int TotalCount) Search(
        string? query, Guid? chapterId, int page, int pageSize) {

        var q = _context.Members.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query)) {
            if (int.TryParse(query, out var memberNum)) {
                q = q.Where(m => m.MemberNumber == memberNum);
            } else {
                q = q.Where(m => m.FirstName.Contains(query)
                    || m.LastName.Contains(query)
                    || (m.EMail != null && m.EMail.Contains(query)));
            }
        }

        if (chapterId.HasValue)
            q = q.Where(m => m.ChapterId == chapterId.Value);

        var totalCount = q.Count();
        var items = q.OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public void Insert(Member member) => _context.Insert(member);

    public void Update(Member member) {
        _context.Members
            .Where(m => m.Id == member.Id)
            .Set(m => m.AdmissionReference, member.AdmissionReference)
            .Set(m => m.FirstName, member.FirstName)
            .Set(m => m.LastName, member.LastName)
            .Set(m => m.Street, member.Street)
            .Set(m => m.Country, member.Country)
            .Set(m => m.PostCode, member.PostCode)
            .Set(m => m.City, member.City)
            .Set(m => m.Phone, member.Phone)
            .Set(m => m.EMail, member.EMail)
            .Set(m => m.DateOfBirth, member.DateOfBirth)
            .Set(m => m.Citizenship, member.Citizenship)
            .Set(m => m.MembershipFee, member.MembershipFee)
            .Set(m => m.ReducedFee, member.ReducedFee)
            .Set(m => m.FirstFee, member.FirstFee)
            .Set(m => m.OpenFeeTotal, member.OpenFeeTotal)
            .Set(m => m.ReducedFeeEnd, member.ReducedFeeEnd)
            .Set(m => m.EntryDate, member.EntryDate)
            .Set(m => m.ExitDate, member.ExitDate)
            .Set(m => m.FederalState, member.FederalState)
            .Set(m => m.County, member.County)
            .Set(m => m.Municipality, member.Municipality)
            .Set(m => m.IsPending, member.IsPending)
            .Set(m => m.HasVotingRights, member.HasVotingRights)
            .Set(m => m.ReceivesSurveys, member.ReceivesSurveys)
            .Set(m => m.ReceivesActions, member.ReceivesActions)
            .Set(m => m.ReceivesNewsletter, member.ReceivesNewsletter)
            .Set(m => m.PostBounce, member.PostBounce)
            .Set(m => m.ChapterId, member.ChapterId)
            .Set(m => m.ResidenceAdministrativeDivisionId, member.ResidenceAdministrativeDivisionId)
            .Set(m => m.LastImportedAt, member.LastImportedAt)
            .Update();
    }

    public void InsertImportLog(MemberImportLog log) => _context.Insert(log);

    public (List<MemberImportLog> Items, int TotalCount) GetImportHistory(int page, int pageSize) {
        var q = _context.MemberImportLogs.AsQueryable();
        var totalCount = q.Count();
        var items = q.OrderByDescending(l => l.ImportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return (items, totalCount);
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Data/Quartermaster.Data.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Quartermaster.Data/Members/MemberRepository.cs
git commit -m "feat: add MemberRepository with search, upsert, and import log"
```

---

## Task 3: Chapter ExternalCode Seeding

**Files:**
- Modify: `Quartermaster.Data/Chapters/ChapterRepository.cs`

This task extends `SupplementDefaults()` to:
1. Add `ExternalCode` to the 16 existing state chapters
2. Create Bezirk-level chapters as children of state chapters
3. Create Kreis-level chapters as children of Bezirk (or state if no Bezirk)
4. Add an "Ausland" chapter for members abroad
5. Add a `FindByExternalCode()` method for import-time lookups

**Important context:** The chapter data comes from the CSV file at `/media/SMB/sampledata/system_export_chapters.csv`. The unique combinations are ~220 rows. The state code mapping is:

| CSV Code | State Name (existing chapter) |
|----------|------|
| BW | Baden-Württemberg |
| BY | Bayern |
| BE | Berlin |
| BB | Brandenburg |
| HB | Bremen |
| HH | Hamburg |
| HE | Hessen |
| MV | Mecklenburg-Vorpommern |
| NI | Niedersachsen |
| NW | Nordrhein-Westfalen |
| RP | Rheinland-Pfalz |
| SL | Saarland |
| SN | Sachsen |
| ST | Sachsen-Anhalt |
| SH | Schleswig-Holstein |
| TH | Thüringen |
| Ausland | (no existing chapter) |

- [ ] **Step 1: Add FindByExternalCode and state code mapping**

In `Quartermaster.Data/Chapters/ChapterRepository.cs`, add a new method and a static dictionary. Add `using System.Collections.Generic;` if not already present.

Add this static field to the class:

```csharp
private static readonly Dictionary<string, string> ExternalCodeToShortCode = new() {
    ["BW"] = "bw", ["BY"] = "by", ["BE"] = "be", ["BB"] = "bb",
    ["HB"] = "hb", ["HH"] = "hh", ["HE"] = "he", ["MV"] = "mv",
    ["NI"] = "nds", ["NW"] = "nrw", ["RP"] = "rlp", ["SL"] = "sl",
    ["SN"] = "sn", ["ST"] = "st", ["SH"] = "sh", ["TH"] = "th"
};
```

Add this method:

```csharp
public List<Chapter> GetByExternalCode(string externalCode)
    => _context.Chapters.Where(c => c.ExternalCode == externalCode).ToList();

public Chapter? FindByExternalCodeAndParent(string externalCode, Guid? parentChapterId)
    => _context.Chapters
        .Where(c => c.ExternalCode == externalCode && c.ParentChapterId == parentChapterId)
        .FirstOrDefault();
```

- [ ] **Step 2: Extend SupplementDefaults to set ExternalCode on state chapters and seed sub-chapters**

In `SupplementDefaults()`, after the existing `foreach` loop that creates state chapters, add:

```csharp
// Set ExternalCode on state chapters
var allChapters = GetAll();
foreach (var chapter in allChapters) {
    if (chapter.ShortCode != null) {
        var extCode = ExternalCodeToShortCode
            .FirstOrDefault(kv => kv.Value == chapter.ShortCode).Key;
        if (extCode != null && chapter.ExternalCode == null) {
            _context.Chapters
                .Where(c => c.Id == chapter.Id)
                .Set(c => c.ExternalCode, extCode)
                .Update();
            chapter.ExternalCode = extCode;
        }
    }
}

// Set ExternalCode "de" on Bundesverband
_context.Chapters
    .Where(c => c.Id == bundesverband.Id)
    .Set(c => c.ExternalCode, "de")
    .Update();

// Create Ausland chapter
var ausland = new Chapter {
    Id = Guid.NewGuid(),
    Name = "Ausland",
    AdministrativeDivisionId = null,
    ParentChapterId = bundesverband.Id,
    ShortCode = null,
    ExternalCode = "Ausland"
};
Create(ausland);

// Seed Bezirk and Kreis chapters from known chapter structure
SeedSubChapters(bundesverband.Id);
```

- [ ] **Step 3: Add the SeedSubChapters method**

Add this method to `ChapterRepository`. It contains the hardcoded chapter structure derived from the CSV export. The data is grouped by state for readability. Each tuple is `(bezirkName, kreisName)` where either can be null.

```csharp
private void SeedSubChapters(Guid bundesverbandId) {
    var stateChapters = _context.Chapters
        .Where(c => c.ParentChapterId == bundesverbandId && c.ExternalCode != null && c.ExternalCode != "Ausland")
        .ToList()
        .ToDictionary(c => c.ExternalCode!);

    // Sub-chapter definitions: (LV, Bezirk, Kreis) — NULL means level doesn't exist
    var subChapters = new List<(string Lv, string? Bezirk, string? Kreis)> {
        // Baden-Württemberg
        ("BW", "BW.FR", null),
        ("BW", "BW.KA", null), ("BW", "BW.KA", "BW.KA.BAD"), ("BW", "BW.KA", "BW.KA.FDS"), ("BW", "BW.KA", "BW.KA.HD"),
        ("BW", "BW.S", null), ("BW", "BW.S", "BW.S.S"),
        ("BW", "BW.TÜ", null), ("BW", "BW.TÜ", "BW.TÜ.UL"),
        ("BW", "BzV Südwürtemberg", null),
        // Bayern
        ("BY", "Bezirksverband Mittelfranken", null), ("BY", "Bezirksverband Mittelfranken", "Kreisverband Nürnberg"),
        ("BY", "Bezirksverband Mittelfranken", "Kreisverband Nürnberger Land"),
        ("BY", "Bezirksverband Mittelfranken", "KV Erlangen und Erlangen-Höchstadt"),
        ("BY", "Bezirksverband Mittelfranken", "KV Weißenburg-Gunzenhausen"),
        ("BY", "Bezirksverband Niederbayern", null), ("BY", "Bezirksverband Niederbayern", "KV Landshut"),
        ("BY", "Bezirksverband Oberbayern", null), ("BY", "Bezirksverband Oberbayern", "Kreisverband Berchtesgadener Land"),
        ("BY", "Bezirksverband Oberbayern", "Kreisverband Ebersberg"), ("BY", "Bezirksverband Oberbayern", "Kreisverband Ingolstadt"),
        ("BY", "Bezirksverband Oberbayern", "Kreisverband Landsberg am Lech"),
        ("BY", "Bezirksverband Oberbayern", "Kreisverband Mühldorf am Inn"),
        ("BY", "Bezirksverband Oberbayern", "Kreisverband München-Land"),
        ("BY", "Bezirksverband Oberbayern", "Kreisverband München-Stadt"),
        ("BY", "Bezirksverband Oberbayern", "Kreisverband Rosenheim"),
        ("BY", "Bezirksverband Oberbayern", "Kreisverband Starnberg"),
        ("BY", "Bezirksverband Oberbayern", "Kreisverband Traunstein"),
        ("BY", "Bezirksverband Oberfranken", null), ("BY", "Bezirksverband Oberfranken", "Kreisverband Bamberg"),
        ("BY", "Bezirksverband Oberfranken", "Kreisverband Bayreuth"),
        ("BY", "Bezirksverband Oberfranken", "Kreisverband Hof-Wunsiedel"),
        ("BY", "Bezirksverband Oberfranken", "Kreisverband Kronach"),
        ("BY", "Bezirksverband Oberpfalz", null), ("BY", "Bezirksverband Oberpfalz", "Kreisverband Neumarkt in der Oberpfalz"),
        ("BY", "Bezirksverband Oberpfalz", "Kreisverband Regensburg"),
        ("BY", "Bezirksverband Oberpfalz", "Kreisverband Schwandorf"),
        ("BY", "Bezirksverband Schwaben", null), ("BY", "Bezirksverband Schwaben", "Kreisverband Allgäu-Bodensee"),
        ("BY", "Bezirksverband Schwaben", "Kreisverband Kaufbeuren-Ostallgäu"),
        ("BY", "Bezirksverband Schwaben", "Kreisverband Neu-Ulm"),
        ("BY", "Bezirksverband Schwaben", "KV Günzburg"),
        ("BY", "Bezirksverband Unterfranken", null),
        // Berlin
        ("BE", "B-Charlottenburg-Wilmersdorf", null), ("BE", "B-Friedrichshain-Kreuzberg", null),
        ("BE", "B-Lichtenberg", null), ("BE", "B-Marzahn-Hellersdorf", null),
        ("BE", "B-Mitte", null), ("BE", "B-Neukölln", null),
        ("BE", "B-Pankow", null), ("BE", "B-Reinickendorf", null),
        ("BE", "B-Spandau", null), ("BE", "B-Steglitz-Zehlendorf", null),
        ("BE", "B-Tempelhof-Schöneberg", null), ("BE", "B-Treptow-Köpenick", null),
        // Brandenburg
        ("BB", null, "Märkisch-Oderland"), ("BB", null, "Potsdam"),
        ("BB", null, "RV DOS"), ("BB", null, "RV NORD"),
        ("BB", null, "RV SÜD"), ("BB", null, "RV WEST"),
        ("BB", null, "Teltow-Fläming"),
        // Bremen
        ("HB", null, "Bremerhaven"),
        // Hamburg
        ("HH", "HH-Altona", null), ("HH", "HH-Bergedorf", null),
        ("HH", "HH-Eimsbüttel", null), ("HH", "HH-Harburg", null),
        ("HH", "HH-Mitte", null), ("HH", "HH-Nord", null),
        ("HH", "HH-Wandsbek", null),
        // Hessen
        ("HE", "Mittelhessen", null), ("HE", "Mittelhessen", "Gießen"),
        ("HE", "Mittelhessen", "Lahn-Dill-Kreis"), ("HE", "Mittelhessen", "Limburg-Weilburg"),
        ("HE", "Mittelhessen", "Marburg-Biedenkopf"), ("HE", "Mittelhessen", "Vogelsbergkreis"),
        ("HE", "Nordhessen", null), ("HE", "Nordhessen", "Fulda"),
        ("HE", "Nordhessen", "Kassel"), ("HE", "Nordhessen", "Kassel Stadt"),
        ("HE", "Nordhessen", "Schwalm-Eder-Kreis"), ("HE", "Nordhessen", "Waldeck-Frankenberg"),
        ("HE", "Nordhessen", "Werra-Meißner-Kreis"),
        ("HE", "Südhessen", null), ("HE", "Südhessen", "Bergstraße"),
        ("HE", "Südhessen", "Darmstadt-Dieburg"), ("HE", "Südhessen", "Darmstadt Stadt"),
        ("HE", "Südhessen", "Frankfurt Stadt"), ("HE", "Südhessen", "Groß-Gerau"),
        ("HE", "Südhessen", "Hochtaunuskreis"), ("HE", "Südhessen", "Main-Kinzig-Kreis"),
        ("HE", "Südhessen", "Main-Taunus-Kreis"), ("HE", "Südhessen", "Odenwaldkreis"),
        ("HE", "Südhessen", "Offenbach"), ("HE", "Südhessen", "Rheingau-Taunus-Kreis"),
        ("HE", "Südhessen", "Wetteraukreis"), ("HE", "Südhessen", "Wiesbaden"),
        // Niedersachsen
        ("NI", null, "Cloppenburg"), ("NI", null, "Diepholz"),
        ("NI", null, "Göttingen"), ("NI", null, "Hameln-Pyrmont"),
        ("NI", null, "Nienburg-Schaumburg"), ("NI", null, "Nordost"),
        ("NI", null, "Oldenburg"), ("NI", null, "Osnabrück"),
        ("NI", null, "Region Hannover"), ("NI", null, "Stade"),
        ("NI", null, "Stadt Braunschweig"), ("NI", null, "Stadt Oldenburg"),
        ("NI", null, "Stadt Wolfsburg"), ("NI", null, "Südheide"),
        ("NI", null, "Wolfenbüttel-Salzgitter"),
        // Nordrhein-Westfalen
        ("NW", "RB Arnsberg", null), ("NW", "RB Arnsberg", "Bochum"),
        ("NW", "RB Arnsberg", "Dortmund"), ("NW", "RB Arnsberg", "Ennepe-Ruhr-Kreis"),
        ("NW", "RB Arnsberg", "Hagen"), ("NW", "RB Arnsberg", "Hamm"),
        ("NW", "RB Arnsberg", "Herne"), ("NW", "RB Arnsberg", "Hochsauerlandkreis"),
        ("NW", "RB Arnsberg", "Märkischer Kreis"), ("NW", "RB Arnsberg", "Olpe"),
        ("NW", "RB Arnsberg", "Siegen-Wittgenstein"), ("NW", "RB Arnsberg", "Soest"),
        ("NW", "RB Arnsberg", "Unna"),
        ("NW", "RB Detmold", null), ("NW", "RB Detmold", "Bielefeld"),
        ("NW", "RB Detmold", "Gütersloh"), ("NW", "RB Detmold", "Herford"),
        ("NW", "RB Detmold", "Höxter"), ("NW", "RB Detmold", "Lippe"),
        ("NW", "RB Detmold", "Minden-Lübbecke"), ("NW", "RB Detmold", "Paderborn"),
        ("NW", "RB Düsseldorf", null), ("NW", "RB Düsseldorf", "Duisburg"),
        ("NW", "RB Düsseldorf", "Düsseldorf"), ("NW", "RB Düsseldorf", "Essen"),
        ("NW", "RB Düsseldorf", "Kleve"), ("NW", "RB Düsseldorf", "Krefeld"),
        ("NW", "RB Düsseldorf", "Mettmann"), ("NW", "RB Düsseldorf", "Mönchengladbach"),
        ("NW", "RB Düsseldorf", "Mülheim"), ("NW", "RB Düsseldorf", "Oberhausen"),
        ("NW", "RB Düsseldorf", "Remscheid"), ("NW", "RB Düsseldorf", "Rhein-Kreis Neuss"),
        ("NW", "RB Düsseldorf", "Solingen"), ("NW", "RB Düsseldorf", "Viersen"),
        ("NW", "RB Düsseldorf", "Wesel"), ("NW", "RB Düsseldorf", "Wuppertal"),
        ("NW", "RB Köln", null), ("NW", "RB Köln", "Aachen"),
        ("NW", "RB Köln", "Bonn"), ("NW", "RB Köln", "Düren"),
        ("NW", "RB Köln", "Euskirchen"), ("NW", "RB Köln", "Heinsberg"),
        ("NW", "RB Köln", "Köln"), ("NW", "RB Köln", "Leverkusen"),
        ("NW", "RB Köln", "Oberbergischer Kreis"), ("NW", "RB Köln", "Rhein-Erft-Kreis"),
        ("NW", "RB Köln", "Rheinisch-Bergischer Kreis"), ("NW", "RB Köln", "Rhein-Sieg-Kreis"),
        ("NW", "RB Münster", null), ("NW", "RB Münster", "Borken"),
        ("NW", "RB Münster", "Bottrop"), ("NW", "RB Münster", "Coesfeld"),
        ("NW", "RB Münster", "Gelsenkirchen"), ("NW", "RB Münster", "Münster"),
        ("NW", "RB Münster", "Recklinghausen"), ("NW", "RB Münster", "Steinfurt"),
        ("NW", "RB Münster", "Warendorf"),
        // Rheinland-Pfalz
        ("RP", null, "KV Koblenz/Mayen-Koblenz"), ("RP", null, "KV Rhein-Pfalz"),
        ("RP", null, "KV Südpfalz"), ("RP", null, "KV Südwestpfalz"),
        ("RP", null, "KV Trier/Trier-Saarburg"),
        ("RP", null, "vKV Ahrweiler"), ("RP", null, "vKV Altenkirchen"),
        ("RP", null, "vKV Alzey-Worms"), ("RP", null, "vKV Bad Dürkheim"),
        ("RP", null, "vKV Bad Kreuznach"), ("RP", null, "vKV Bernkastel-Wittlich"),
        ("RP", null, "vKV Birkenfeld"), ("RP", null, "vKV Cochem-Zell"),
        ("RP", null, "vKV Donnersbergkreis"), ("RP", null, "vKV Kaiserslautern"),
        ("RP", null, "vKV Kusel"), ("RP", null, "vKV Mainz"),
        ("RP", null, "vKV Mainz-Bingen"), ("RP", null, "vKV Neustadt"),
        ("RP", null, "vKV Neuwied"), ("RP", null, "vKV Rhein-Hunsrück-Kreis"),
        ("RP", null, "vKV Rhein-Lahn-Kreis"), ("RP", null, "vKV St. Kaiserslautern"),
        ("RP", null, "vKV Westerwaldkreis"), ("RP", null, "vKV Worms"),
        // Saarland
        ("SL", null, "Merzig-Wadern"), ("SL", null, "Neunkirchen"),
        ("SL", null, "Saarbrücken"), ("SL", null, "Saarlouis"),
        ("SL", null, "Saarpfalz-Kreis"), ("SL", null, "St. Wendel"),
        // Schleswig-Holstein
        ("SH", null, "KV Kiel"),
        ("SH", null, "vKV Dithmarschen"), ("SH", null, "vKV Neumünster"),
        ("SH", null, "vKV Nordfriesland"), ("SH", null, "vKV Pinneberg"),
        ("SH", null, "vKV Plön"), ("SH", null, "vKV Rendsburg-Eckernförde"),
        ("SH", null, "vKV Segeberg"), ("SH", null, "vKV Südholstein"),
        // Sachsen
        ("SN", null, "Chemnitz"), ("SN", null, "Dresden"),
        ("SN", null, "Leipzig"), ("SN", null, "Meißen"),
        ("SN", null, "vKV Bautzen"), ("SN", null, "vKV Erzgebirge"),
        ("SN", null, "vKV Görlitz"), ("SN", null, "vKV Leipziger Land"),
        ("SN", null, "vKV Mittelsachsen"), ("SN", null, "vKV Nordsachsen"),
        ("SN", null, "vKV Sächsische Schweiz"), ("SN", null, "vKV Vogtland"),
        ("SN", null, "vKV Zwickau"),
        // Sachsen-Anhalt
        ("ST", null, "KV Börde"),
        ("ST", null, "RV Altmark"),
        ("ST", null, "VKV Anhalt-Bitterfeld"), ("ST", null, "VKV Burgenlandkreis"),
        ("ST", null, "VKV Dessau"), ("ST", null, "VKV Halle"),
        ("ST", null, "VKV Harz"), ("ST", null, "VKV Jerichower Land"),
        ("ST", null, "VKV Magdeburg"), ("ST", null, "VKV Mansfeld-Südharz"),
        ("ST", null, "VKV Saalekreis"), ("ST", null, "VKV Salzlandkreis"),
        ("ST", null, "VKV Wittenberg"),
        // Thüringen
        ("TH", "Mitte-Thüringen", null), ("TH", "Mitte-Thüringen", "Erfurt"),
        ("TH", "Mitte-Thüringen", "Ilm-Kreis"), ("TH", "Mitte-Thüringen", "vKV SRU"),
        ("TH", "Mitte-Thüringen", "Weimar"),
        ("TH", "Nord-Thüringen", null), ("TH", "Nord-Thüringen", "vKV KYF"),
        ("TH", "Nord-Thüringen", "vKV NDH"), ("TH", "Nord-Thüringen", "vKV SÖM"),
        ("TH", "Nord-Thüringen", "vKV UH"),
        ("TH", "Ost-Thüringen", null), ("TH", "Ost-Thüringen", "Gera"),
        ("TH", "Ost-Thüringen", "Jena"), ("TH", "Ost-Thüringen", "vKV Greiz"),
        ("TH", "Ost-Thüringen", "vKV SHKSOK"),
        ("TH", "Süd-Thüringen", null), ("TH", "Süd-Thüringen", "Schmalkalden-Meiningen"),
        ("TH", "Süd-Thüringen", "vKV SHLHibuSON"),
        ("TH", "West-Thüringen", null), ("TH", "West-Thüringen", "Gotha"),
        ("TH", "West-Thüringen", "Wartburgkreis"),
        // Mecklenburg-Vorpommern
        ("MV", null, "WM"),
        // Hessen (some Kreis entries appear without Bezirk in the data)
        ("HE", null, "Frankfurt Stadt"), ("HE", null, "Gießen"),
        ("HE", null, "Kassel Stadt"), ("HE", null, "Limburg-Weilburg"),
        ("HE", null, "Rheingau-Taunus-Kreis"), ("HE", null, "Vogelsbergkreis"),
    };

    // Track created Bezirk chapters to avoid duplicates
    var createdBezirke = new Dictionary<string, Chapter>();

    foreach (var (lv, bezirk, kreis) in subChapters) {
        if (!stateChapters.TryGetValue(lv, out var stateChapter))
            continue;

        Chapter? parentForKreis = stateChapter;

        // Create Bezirk if specified and not yet created
        if (bezirk != null) {
            var bezirkKey = $"{lv}|{bezirk}";
            if (!createdBezirke.TryGetValue(bezirkKey, out var bezirkChapter)) {
                // Check if already exists in DB (idempotent)
                bezirkChapter = FindByExternalCodeAndParent(bezirk, stateChapter.Id);
                if (bezirkChapter == null) {
                    bezirkChapter = new Chapter {
                        Id = Guid.NewGuid(),
                        Name = bezirk,
                        ParentChapterId = stateChapter.Id,
                        ExternalCode = bezirk
                    };
                    Create(bezirkChapter);
                }
                createdBezirke[bezirkKey] = bezirkChapter;
            }
            parentForKreis = bezirkChapter;
        }

        // Create Kreis if specified
        if (kreis != null) {
            var existing = FindByExternalCodeAndParent(kreis, parentForKreis.Id);
            if (existing == null) {
                Create(new Chapter {
                    Id = Guid.NewGuid(),
                    Name = kreis,
                    ParentChapterId = parentForKreis.Id,
                    ExternalCode = kreis
                });
            }
        }
    }
}
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Data/Quartermaster.Data.csproj`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Quartermaster.Data/Chapters/ChapterRepository.cs
git commit -m "feat: seed Bezirk and Kreis chapters with ExternalCode from CSV data"
```

---

## Task 4: API DTOs

**Files:**
- Create: `Quartermaster.Api/Members/MemberDTO.cs`
- Create: `Quartermaster.Api/Members/MemberDetailDTO.cs`
- Create: `Quartermaster.Api/Members/MemberSearchRequest.cs`
- Create: `Quartermaster.Api/Members/MemberSearchResponse.cs`
- Create: `Quartermaster.Api/Members/MemberImportLogDTO.cs`
- Create: `Quartermaster.Api/Members/MemberImportLogListResponse.cs`

- [ ] **Step 1: Create MemberDTO**

Create `Quartermaster.Api/Members/MemberDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.Members;

public class MemberDTO {
    public Guid Id { get; set; }
    public int MemberNumber { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PostCode { get; set; }
    public string? City { get; set; }
    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public DateTime? EntryDate { get; set; }
    public DateTime? ExitDate { get; set; }
    public bool IsPending { get; set; }
    public bool HasVotingRights { get; set; }
}
```

- [ ] **Step 2: Create MemberDetailDTO**

Create `Quartermaster.Api/Members/MemberDetailDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.Members;

public class MemberDetailDTO {
    public Guid Id { get; set; }
    public int MemberNumber { get; set; }
    public string? AdmissionReference { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Street { get; set; }
    public string? Country { get; set; }
    public string? PostCode { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? EMail { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Citizenship { get; set; }
    public decimal MembershipFee { get; set; }
    public decimal ReducedFee { get; set; }
    public decimal? FirstFee { get; set; }
    public decimal? OpenFeeTotal { get; set; }
    public DateTime? ReducedFeeEnd { get; set; }
    public DateTime? EntryDate { get; set; }
    public DateTime? ExitDate { get; set; }
    public string? FederalState { get; set; }
    public string? County { get; set; }
    public string? Municipality { get; set; }
    public bool IsPending { get; set; }
    public bool HasVotingRights { get; set; }
    public bool ReceivesSurveys { get; set; }
    public bool ReceivesActions { get; set; }
    public bool ReceivesNewsletter { get; set; }
    public bool PostBounce { get; set; }
    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public Guid? ResidenceAdministrativeDivisionId { get; set; }
    public string ResidenceAdministrativeDivisionName { get; set; } = "";
    public Guid? UserId { get; set; }
    public DateTime LastImportedAt { get; set; }
}
```

- [ ] **Step 3: Create MemberSearchRequest and MemberSearchResponse**

Create `Quartermaster.Api/Members/MemberSearchRequest.cs`:

```csharp
using System;

namespace Quartermaster.Api.Members;

public class MemberSearchRequest {
    public string? Query { get; set; }
    public Guid? ChapterId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
```

Create `Quartermaster.Api/Members/MemberSearchResponse.cs`:

```csharp
using System.Collections.Generic;

namespace Quartermaster.Api.Members;

public class MemberSearchResponse {
    public List<MemberDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
```

- [ ] **Step 4: Create MemberImportLogDTO and MemberImportLogListResponse**

Create `Quartermaster.Api/Members/MemberImportLogDTO.cs`:

```csharp
using System;

namespace Quartermaster.Api.Members;

public class MemberImportLogDTO {
    public Guid Id { get; set; }
    public DateTime ImportedAt { get; set; }
    public string FileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public int TotalRecords { get; set; }
    public int NewRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int ErrorCount { get; set; }
    public string? Errors { get; set; }
    public long DurationMs { get; set; }
}
```

Create `Quartermaster.Api/Members/MemberImportLogListResponse.cs`:

```csharp
using System.Collections.Generic;

namespace Quartermaster.Api.Members;

public class MemberImportLogListResponse {
    public List<MemberImportLogDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Api/Quartermaster.Api.csproj`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Quartermaster.Api/Members/
git commit -m "feat: add Member API DTOs for list, detail, search, and import log"
```

---

## Task 5: CSV Parsing and MemberImportService

**Files:**
- Modify: `Quartermaster.Server/Quartermaster.Server.csproj` (add CsvHelper package)
- Create: `Quartermaster.Server/Members/MemberCsvRecord.cs`
- Create: `Quartermaster.Server/Members/MemberImportService.cs`

- [ ] **Step 1: Add CsvHelper NuGet package**

Run: `dotnet add /media/SMB/Quartermaster/Quartermaster.Server/Quartermaster.Server.csproj package CsvHelper`

- [ ] **Step 2: Create MemberCsvRecord**

Create `Quartermaster.Server/Members/MemberCsvRecord.cs`:

```csharp
using CsvHelper.Configuration;

namespace Quartermaster.Server.Members;

public class MemberCsvRecord {
    public int USER_Mitgliedsnummer { get; set; }
    public string? USER_refAufnahme { get; set; }
    public string Name1 { get; set; } = "";
    public string Name2 { get; set; } = "";
    public string? LieferStrasse { get; set; }
    public string? LieferLand { get; set; }
    public string? LieferPLZ { get; set; }
    public string? LieferOrt { get; set; }
    public string? Telefon { get; set; }
    public string? EMail { get; set; }
    public string? USER_LV { get; set; }
    public string? USER_Bezirk { get; set; }
    public string? USER_Kreis { get; set; }
    public string? USER_Beitrag { get; set; }
    public string? USER_redBeitrag { get; set; }
    public string? USER_Umfragen { get; set; }
    public string? USER_Aktionen { get; set; }
    public string? USER_Newsletter { get; set; }
    public string? USER_Geburtsdatum { get; set; }
    public string? USER_Postbounce { get; set; }
    public string? USER_Bundesland { get; set; }
    public string? USER_Eintrittsdatum { get; set; }
    public string? USER_Austrittsdatum { get; set; }
    public string? USER_Erstbeitrag { get; set; }
    public string? USER_Landkreis { get; set; }
    public string? USER_Gemeinde { get; set; }
    public string? USER_Staatsbuergerschaft { get; set; }
    public string? USER_zStimmberechtigung { get; set; }
    public string? USER_zoffenerbeitragtotal { get; set; }
    public string? USER_redBeitragEnde { get; set; }
    public string? USER_Schwebend { get; set; }
}

public sealed class MemberCsvRecordMap : ClassMap<MemberCsvRecord> {
    public MemberCsvRecordMap() {
        Map(m => m.USER_Mitgliedsnummer).Name("USER_Mitgliedsnummer");
        Map(m => m.USER_refAufnahme).Name("USER_refAufnahme");
        Map(m => m.Name1).Name("Name1");
        Map(m => m.Name2).Name("Name2");
        Map(m => m.LieferStrasse).Name("LieferStrasse");
        Map(m => m.LieferLand).Name("LieferLand");
        Map(m => m.LieferPLZ).Name("LieferPLZ");
        Map(m => m.LieferOrt).Name("LieferOrt");
        Map(m => m.Telefon).Name("Telefon");
        Map(m => m.EMail).Name("EMail");
        Map(m => m.USER_LV).Name("USER_LV");
        Map(m => m.USER_Bezirk).Name("USER_Bezirk");
        Map(m => m.USER_Kreis).Name("USER_Kreis");
        Map(m => m.USER_Beitrag).Name("USER_Beitrag");
        Map(m => m.USER_redBeitrag).Name("USER_redBeitrag");
        Map(m => m.USER_Umfragen).Name("USER_Umfragen");
        Map(m => m.USER_Aktionen).Name("USER_Aktionen");
        Map(m => m.USER_Newsletter).Name("USER_Newsletter");
        Map(m => m.USER_Geburtsdatum).Name("USER_Geburtsdatum");
        Map(m => m.USER_Postbounce).Name("USER_Postbounce");
        Map(m => m.USER_Bundesland).Name("USER_Bundesland");
        Map(m => m.USER_Eintrittsdatum).Name("USER_Eintrittsdatum");
        Map(m => m.USER_Austrittsdatum).Name("USER_Austrittsdatum");
        Map(m => m.USER_Erstbeitrag).Name("USER_Erstbeitrag");
        Map(m => m.USER_Landkreis).Name("USER_Landkreis");
        Map(m => m.USER_Gemeinde).Name("USER_Gemeinde");
        Map(m => m.USER_Staatsbuergerschaft).Name("USER_Staatsbuergerschaft");
        Map(m => m.USER_zStimmberechtigung).Name("USER_zStimmberechtigung");
        Map(m => m.USER_zoffenerbeitragtotal).Name("USER_zoffenerbeitragtotal");
        Map(m => m.USER_redBeitragEnde).Name("USER_redBeitragEnde");
        Map(m => m.USER_Schwebend).Name("USER_Schwebend");
    }
}
```

- [ ] **Step 3: Create MemberImportService**

Create `Quartermaster.Server/Members/MemberImportService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.Members;

public class MemberImportService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberImportService> _logger;

    public MemberImportService(IServiceScopeFactory scopeFactory, ILogger<MemberImportService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public static string ComputeFileHash(string filePath) {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    public MemberImportLog ImportFromFile(string filePath) {
        var sw = Stopwatch.StartNew();
        var fileName = Path.GetFileName(filePath);
        var fileHash = ComputeFileHash(filePath);
        var errors = new List<string>();
        int totalRecords = 0, newRecords = 0, updatedRecords = 0;

        using var scope = _scopeFactory.CreateScope();
        var memberRepo = scope.ServiceProvider.GetRequiredService<MemberRepository>();
        var chapterRepo = scope.ServiceProvider.GetRequiredService<ChapterRepository>();
        var adminDivRepo = scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>();

        // Pre-load chapter lookup: all chapters with ExternalCode
        var allChapters = chapterRepo.GetAll();
        var chaptersByExtCode = allChapters
            .Where(c => c.ExternalCode != null)
            .GroupBy(c => c.ExternalCode!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) {
            Delimiter = ";",
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, csvConfig);
        csv.Context.RegisterClassMap<MemberCsvRecordMap>();

        var records = csv.GetRecords<MemberCsvRecord>();
        var now = DateTime.UtcNow;

        foreach (var record in records) {
            totalRecords++;
            try {
                var member = MapRecordToMember(record, chaptersByExtCode, allChapters, adminDivRepo, now);
                var existing = memberRepo.GetByMemberNumber(member.MemberNumber);

                if (existing != null) {
                    member.Id = existing.Id;
                    member.UserId = existing.UserId; // Preserve SSO link
                    // TODO: Audit log — compare existing vs new fields for change tracking
                    memberRepo.Update(member);
                    updatedRecords++;
                } else {
                    memberRepo.Insert(member);
                    newRecords++;
                }
            } catch (Exception ex) {
                errors.Add($"Row {totalRecords}: Member #{record.USER_Mitgliedsnummer} — {ex.Message}");
                _logger.LogWarning(ex, "Failed to import member #{MemberNumber}", record.USER_Mitgliedsnummer);
            }
        }

        sw.Stop();

        var log = new MemberImportLog {
            ImportedAt = now,
            FileName = fileName,
            FileHash = fileHash,
            TotalRecords = totalRecords,
            NewRecords = newRecords,
            UpdatedRecords = updatedRecords,
            ErrorCount = errors.Count,
            Errors = errors.Count > 0 ? JsonSerializer.Serialize(errors) : null,
            DurationMs = sw.ElapsedMilliseconds
        };

        memberRepo.InsertImportLog(log);

        _logger.LogInformation(
            "Import complete: {Total} records ({New} new, {Updated} updated, {Errors} errors) in {Duration}ms",
            totalRecords, newRecords, updatedRecords, errors.Count, sw.ElapsedMilliseconds);

        return log;
    }

    private static Member MapRecordToMember(
        MemberCsvRecord record,
        Dictionary<string, List<Chapter>> chaptersByExtCode,
        List<Chapter> allChapters,
        AdministrativeDivisionRepository adminDivRepo,
        DateTime importedAt) {

        var member = new Member {
            MemberNumber = record.USER_Mitgliedsnummer,
            AdmissionReference = NullIfEmpty(record.USER_refAufnahme),
            FirstName = record.Name2,
            LastName = record.Name1,
            Street = NullIfEmpty(record.LieferStrasse),
            Country = NullIfEmpty(record.LieferLand),
            PostCode = NullIfEmpty(record.LieferPLZ),
            City = NullIfEmpty(record.LieferOrt),
            Phone = NullIfEmpty(record.Telefon),
            EMail = NullIfEmpty(record.EMail),
            DateOfBirth = ParseDateTime(record.USER_Geburtsdatum),
            Citizenship = NullIfEmpty(record.USER_Staatsbuergerschaft),
            MembershipFee = ParseDecimal(record.USER_Beitrag),
            ReducedFee = ParseDecimal(record.USER_redBeitrag),
            FirstFee = ParseNullableDecimal(record.USER_Erstbeitrag),
            OpenFeeTotal = ParseNullableDecimal(record.USER_zoffenerbeitragtotal),
            ReducedFeeEnd = ParseDateTime(record.USER_redBeitragEnde),
            EntryDate = ParseDateTime(record.USER_Eintrittsdatum),
            ExitDate = ParseDateTime(record.USER_Austrittsdatum),
            FederalState = NullIfEmpty(record.USER_Bundesland),
            County = NullIfEmpty(record.USER_Landkreis),
            Municipality = NullIfEmpty(record.USER_Gemeinde),
            IsPending = ParseBool(record.USER_Schwebend),
            HasVotingRights = ParseBool(record.USER_zStimmberechtigung),
            ReceivesSurveys = ParseBool(record.USER_Umfragen),
            ReceivesActions = ParseBool(record.USER_Aktionen),
            ReceivesNewsletter = ParseBool(record.USER_Newsletter),
            PostBounce = ParseBool(record.USER_Postbounce),
            LastImportedAt = importedAt
        };

        // Resolve chapter from CSV hierarchy
        member.ChapterId = ResolveChapter(
            NullIfEmpty(record.USER_LV),
            NullIfEmpty(record.USER_Bezirk),
            NullIfEmpty(record.USER_Kreis),
            chaptersByExtCode, allChapters);

        // Resolve residence administrative division from PLZ
        if (!string.IsNullOrEmpty(member.PostCode)) {
            var (results, _) = adminDivRepo.Search(member.PostCode, 1, 10);
            if (results.Count == 1) {
                member.ResidenceAdministrativeDivisionId = results[0].Id;
            } else if (results.Count > 1 && !string.IsNullOrEmpty(member.City)) {
                var cityMatch = results.FirstOrDefault(r => r.Name.Contains(member.City, StringComparison.OrdinalIgnoreCase));
                member.ResidenceAdministrativeDivisionId = cityMatch?.Id ?? results[0].Id;
            } else if (results.Count > 1) {
                member.ResidenceAdministrativeDivisionId = results[0].Id;
            }
        }

        return member;
    }

    private static Guid? ResolveChapter(
        string? lv, string? bezirk, string? kreis,
        Dictionary<string, List<Chapter>> chaptersByExtCode,
        List<Chapter> allChapters) {

        // Try Kreis first (most specific)
        if (kreis != null && chaptersByExtCode.TryGetValue(kreis, out var kreisChapters)) {
            foreach (var kreisChapter in kreisChapters) {
                var parent = allChapters.FirstOrDefault(c => c.Id == kreisChapter.ParentChapterId);
                if (parent == null)
                    continue;

                if (bezirk != null) {
                    // Kreis parent should be the Bezirk
                    if (parent.ExternalCode == bezirk) {
                        var grandparent = allChapters.FirstOrDefault(c => c.Id == parent.ParentChapterId);
                        if (grandparent?.ExternalCode == lv)
                            return kreisChapter.Id;
                    }
                } else {
                    // No Bezirk — Kreis parent should be the LV
                    if (parent.ExternalCode == lv)
                        return kreisChapter.Id;
                }
            }
        }

        // Try Bezirk
        if (bezirk != null && chaptersByExtCode.TryGetValue(bezirk, out var bezirkChapters)) {
            foreach (var bezirkChapter in bezirkChapters) {
                var parent = allChapters.FirstOrDefault(c => c.Id == bezirkChapter.ParentChapterId);
                if (parent?.ExternalCode == lv)
                    return bezirkChapter.Id;
            }
        }

        // Try LV (state)
        if (lv != null && chaptersByExtCode.TryGetValue(lv, out var lvChapters)) {
            return lvChapters.FirstOrDefault()?.Id;
        }

        return null;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) || value == "NULL" ? null : value.Trim();

    private static DateTime? ParseDateTime(string? value) {
        if (string.IsNullOrWhiteSpace(value) || value == "NULL")
            return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;
        return null;
    }

    private static decimal ParseDecimal(string? value) {
        if (string.IsNullOrWhiteSpace(value) || value == "NULL")
            return 0;
        if (decimal.TryParse(value, CultureInfo.InvariantCulture, out var result))
            return result;
        return 0;
    }

    private static decimal? ParseNullableDecimal(string? value) {
        if (string.IsNullOrWhiteSpace(value) || value == "NULL")
            return null;
        if (decimal.TryParse(value, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static bool ParseBool(string? value)
        => value == "1";
}
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Server/Quartermaster.Server.csproj`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Quartermaster.Server/Quartermaster.Server.csproj Quartermaster.Server/Members/MemberCsvRecord.cs Quartermaster.Server/Members/MemberImportService.cs
git commit -m "feat: add MemberImportService with CsvHelper parsing and chapter resolution"
```

---

## Task 6: MemberImportHostedService and Options Seeding

**Files:**
- Create: `Quartermaster.Server/Members/MemberImportHostedService.cs`
- Modify: `Quartermaster.Data/Options/OptionRepository.cs`
- Modify: `Quartermaster.Server/Program.cs`

- [ ] **Step 1: Add import options to SupplementDefaults**

In `Quartermaster.Data/Options/OptionRepository.cs`, add at the end of `SupplementDefaults()`:

```csharp
AddDefinitionIfNotExists("member_import.file_path",
    "Mitgliederimport: Dateipfad",
    OptionDataType.String, false, "", "");

AddDefinitionIfNotExists("member_import.polling_interval_minutes",
    "Mitgliederimport: Abfrageintervall (Minuten)",
    OptionDataType.Number, false, "", "10");
```

- [ ] **Step 2: Create MemberImportHostedService**

Create `Quartermaster.Server/Members/MemberImportHostedService.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Members;

public class MemberImportHostedService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MemberImportService _importService;
    private readonly ILogger<MemberImportHostedService> _logger;
    private string? _lastFileHash;

    public MemberImportHostedService(
        IServiceScopeFactory scopeFactory,
        MemberImportService importService,
        ILogger<MemberImportHostedService> logger) {
        _scopeFactory = scopeFactory;
        _importService = importService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Member import hosted service started");

        // Wait a bit for the app to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested) {
            try {
                var (filePath, intervalMinutes) = ReadOptions();

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) {
                    var currentHash = MemberImportService.ComputeFileHash(filePath);

                    if (_lastFileHash == null || _lastFileHash != currentHash) {
                        _logger.LogInformation("File change detected, starting import from {Path}", filePath);
                        var log = _importService.ImportFromFile(filePath);
                        _lastFileHash = currentHash;
                        _logger.LogInformation(
                            "Import finished: {Total} total, {New} new, {Updated} updated, {Errors} errors",
                            log.TotalRecords, log.NewRecords, log.UpdatedRecords, log.ErrorCount);
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error in member import polling loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private (string? filePath, double intervalMinutes) ReadOptions() {
        using var scope = _scopeFactory.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        var chapterRepo = scope.ServiceProvider.GetRequiredService<ChapterRepository>();

        var filePath = optionRepo.ResolveValue("member_import.file_path", null, chapterRepo);
        var intervalStr = optionRepo.ResolveValue("member_import.polling_interval_minutes", null, chapterRepo);

        double interval = 10;
        if (double.TryParse(intervalStr, out var parsed) && parsed > 0)
            interval = parsed;

        return (filePath, interval);
    }
}
```

- [ ] **Step 3: Register services in Program.cs**

In `Quartermaster.Server/Program.cs`, add the using at the top:

```csharp
using Quartermaster.Server.Members;
```

After the `DbContext.AddRepositories(builder.Services);` line, add:

```csharp
builder.Services.AddSingleton<MemberImportService>();
builder.Services.AddHostedService<MemberImportHostedService>();
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Server/Quartermaster.Server.csproj`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Quartermaster.Server/Members/MemberImportHostedService.cs Quartermaster.Data/Options/OptionRepository.cs Quartermaster.Server/Program.cs
git commit -m "feat: add MemberImportHostedService with polling and options seeding"
```

---

## Task 7: Server Endpoints

**Files:**
- Create: `Quartermaster.Server/Members/MemberListEndpoint.cs`
- Create: `Quartermaster.Server/Members/MemberDetailEndpoint.cs`
- Create: `Quartermaster.Server/Members/MemberImportTriggerEndpoint.cs`
- Create: `Quartermaster.Server/Members/MemberImportHistoryEndpoint.cs`

- [ ] **Step 1: Create MemberListEndpoint**

Create `Quartermaster.Server/Members/MemberListEndpoint.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Members;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.Members;

public class MemberListEndpoint : Endpoint<MemberSearchRequest, MemberSearchResponse> {
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;

    public MemberListEndpoint(MemberRepository memberRepo, ChapterRepository chapterRepo) {
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/members");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(MemberSearchRequest req, CancellationToken ct) {
        var (items, totalCount) = _memberRepo.Search(req.Query, req.ChapterId, req.Page, req.PageSize);
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var dtos = items.Select(m => new MemberDTO {
            Id = m.Id,
            MemberNumber = m.MemberNumber,
            FirstName = m.FirstName,
            LastName = m.LastName,
            PostCode = m.PostCode,
            City = m.City,
            ChapterId = m.ChapterId,
            ChapterName = m.ChapterId.HasValue && chapters.TryGetValue(m.ChapterId.Value, out var name) ? name : "",
            EntryDate = m.EntryDate,
            ExitDate = m.ExitDate,
            IsPending = m.IsPending,
            HasVotingRights = m.HasVotingRights
        }).ToList();

        await SendAsync(new MemberSearchResponse {
            Items = dtos,
            TotalCount = totalCount
        }, cancellation: ct);
    }
}
```

- [ ] **Step 2: Create MemberDetailEndpoint**

Create `Quartermaster.Server/Members/MemberDetailEndpoint.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Members;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.Members;

public class MemberDetailRequest {
    public Guid Id { get; set; }
}

public class MemberDetailEndpoint : Endpoint<MemberDetailRequest, MemberDetailDTO> {
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;

    public MemberDetailEndpoint(
        MemberRepository memberRepo,
        ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo) {
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
    }

    public override void Configure() {
        Get("/api/members/{Id}");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(MemberDetailRequest req, CancellationToken ct) {
        var member = _memberRepo.Get(req.Id);
        if (member == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var chapterName = "";
        if (member.ChapterId.HasValue) {
            var chapter = _chapterRepo.Get(member.ChapterId.Value);
            if (chapter != null)
                chapterName = chapter.Name;
        }

        var adminDivName = "";
        if (member.ResidenceAdministrativeDivisionId.HasValue) {
            var div = _adminDivRepo.Get(member.ResidenceAdministrativeDivisionId.Value);
            if (div != null)
                adminDivName = div.Name;
        }

        await SendAsync(new MemberDetailDTO {
            Id = member.Id,
            MemberNumber = member.MemberNumber,
            AdmissionReference = member.AdmissionReference,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Street = member.Street,
            Country = member.Country,
            PostCode = member.PostCode,
            City = member.City,
            Phone = member.Phone,
            EMail = member.EMail,
            DateOfBirth = member.DateOfBirth,
            Citizenship = member.Citizenship,
            MembershipFee = member.MembershipFee,
            ReducedFee = member.ReducedFee,
            FirstFee = member.FirstFee,
            OpenFeeTotal = member.OpenFeeTotal,
            ReducedFeeEnd = member.ReducedFeeEnd,
            EntryDate = member.EntryDate,
            ExitDate = member.ExitDate,
            FederalState = member.FederalState,
            County = member.County,
            Municipality = member.Municipality,
            IsPending = member.IsPending,
            HasVotingRights = member.HasVotingRights,
            ReceivesSurveys = member.ReceivesSurveys,
            ReceivesActions = member.ReceivesActions,
            ReceivesNewsletter = member.ReceivesNewsletter,
            PostBounce = member.PostBounce,
            ChapterId = member.ChapterId,
            ChapterName = chapterName,
            ResidenceAdministrativeDivisionId = member.ResidenceAdministrativeDivisionId,
            ResidenceAdministrativeDivisionName = adminDivName,
            UserId = member.UserId,
            LastImportedAt = member.LastImportedAt
        }, cancellation: ct);
    }
}
```

- [ ] **Step 3: Create MemberImportTriggerEndpoint**

Create `Quartermaster.Server/Members/MemberImportTriggerEndpoint.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Members;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Members;

public class MemberImportTriggerEndpoint : EndpointWithoutRequest<MemberImportLogDTO> {
    private readonly MemberImportService _importService;
    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;

    public MemberImportTriggerEndpoint(
        MemberImportService importService,
        OptionRepository optionRepo,
        ChapterRepository chapterRepo) {
        _importService = importService;
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/members/import");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var filePath = _optionRepo.ResolveValue("member_import.file_path", null, _chapterRepo);

        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) {
            ThrowError("Import file path is not configured or file does not exist.");
            return;
        }

        var log = _importService.ImportFromFile(filePath);

        await SendAsync(new MemberImportLogDTO {
            Id = log.Id,
            ImportedAt = log.ImportedAt,
            FileName = log.FileName,
            FileHash = log.FileHash,
            TotalRecords = log.TotalRecords,
            NewRecords = log.NewRecords,
            UpdatedRecords = log.UpdatedRecords,
            ErrorCount = log.ErrorCount,
            Errors = log.Errors,
            DurationMs = log.DurationMs
        }, cancellation: ct);
    }
}
```

- [ ] **Step 4: Create MemberImportHistoryEndpoint**

Create `Quartermaster.Server/Members/MemberImportHistoryEndpoint.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Members;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.Members;

public class MemberImportHistoryRequest {
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class MemberImportHistoryEndpoint
    : Endpoint<MemberImportHistoryRequest, MemberImportLogListResponse> {

    private readonly MemberRepository _memberRepo;

    public MemberImportHistoryEndpoint(MemberRepository memberRepo) {
        _memberRepo = memberRepo;
    }

    public override void Configure() {
        Get("/api/members/import/history");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(MemberImportHistoryRequest req, CancellationToken ct) {
        var (items, totalCount) = _memberRepo.GetImportHistory(req.Page, req.PageSize);

        var dtos = items.Select(l => new MemberImportLogDTO {
            Id = l.Id,
            ImportedAt = l.ImportedAt,
            FileName = l.FileName,
            FileHash = l.FileHash,
            TotalRecords = l.TotalRecords,
            NewRecords = l.NewRecords,
            UpdatedRecords = l.UpdatedRecords,
            ErrorCount = l.ErrorCount,
            Errors = l.Errors,
            DurationMs = l.DurationMs
        }).ToList();

        await SendAsync(new MemberImportLogListResponse {
            Items = dtos,
            TotalCount = totalCount
        }, cancellation: ct);
    }
}
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Server/Quartermaster.Server.csproj`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Quartermaster.Server/Members/MemberListEndpoint.cs Quartermaster.Server/Members/MemberDetailEndpoint.cs Quartermaster.Server/Members/MemberImportTriggerEndpoint.cs Quartermaster.Server/Members/MemberImportHistoryEndpoint.cs
git commit -m "feat: add member list, detail, import trigger, and import history endpoints"
```

---

## Task 8: Blazor Member List Page

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/MemberList.razor`
- Create: `Quartermaster.Blazor/Pages/Administration/MemberList.razor.cs`

- [ ] **Step 1: Create MemberList.razor**

Create `Quartermaster.Blazor/Pages/Administration/MemberList.razor`:

```razor
@page "/Administration/Members"
@using Quartermaster.Api.Members
@using Quartermaster.Api.Chapters

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3>Mitglieder</h3>
    <a href="/Administration/Members/Import" class="btn btn-sm btn-outline-secondary">
        <i class="bi bi-clock-history"></i> Importverlauf
    </a>
</div>

<div class="mb-3 d-flex gap-3">
    <div class="flex-grow-1" style="max-width: 400px;">
        <input type="text" class="form-control" placeholder="Name oder Mitgliedsnummer..."
               @bind="SearchQuery" @bind:event="oninput" @onkeydown="OnSearchKeyDown" />
    </div>
    <div>
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
        <button class="btn btn-primary" @onclick="() => GoToPage(1)">
            <i class="bi bi-search"></i> Suchen
        </button>
    </div>
</div>

@if (Loading) {
    <div class="d-flex justify-content-center my-4">
        <div class="spinner-border" role="status"></div>
    </div>
} else if (Response != null) {
    <div class="mb-2 text-secondary">
        @Response.TotalCount Mitglieder
    </div>

    <table class="table table-striped table-hover">
        <thead>
            <tr>
                <th>Nr.</th>
                <th>Name</th>
                <th>Ort</th>
                <th>Gliederung</th>
                <th>Eintritt</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var m in Response.Items) {
                <tr>
                    <td>@m.MemberNumber</td>
                    <td><a href="/Administration/Members/@m.Id">@m.FirstName @m.LastName</a></td>
                    <td>@m.PostCode @m.City</td>
                    <td>@m.ChapterName</td>
                    <td>@(m.EntryDate?.ToString("dd.MM.yyyy") ?? "—")</td>
                    <td>
                        @if (m.ExitDate != null) {
                            <span class="badge border border-danger text-danger-emphasis">Ausgetreten</span>
                        } else if (m.IsPending) {
                            <span class="badge border border-warning text-warning-emphasis">Schwebend</span>
                        } else {
                            <span class="badge border border-success text-success-emphasis">Aktiv</span>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>

    <Pagination CurrentPage="CurrentPage" TotalPages="TotalPages" OnPageChanged="GoToPage" />
}
```

- [ ] **Step 2: Create MemberList.razor.cs**

Create `Quartermaster.Blazor/Pages/Administration/MemberList.razor.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Members;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MemberList {
    [Inject]
    public required HttpClient Http { get; set; }

    private List<ChapterDTO>? Chapters;
    private MemberSearchResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private string? SearchQuery;
    private Guid? SelectedChapterId;

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

    private async Task OnSearchKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter") {
            CurrentPage = 1;
            await Search();
        }
    }

    private async Task GoToPage(int selectedPage) {
        CurrentPage = selectedPage;
        await Search();
    }

    private async Task Search() {
        Loading = true;
        StateHasChanged();

        var url = $"/api/members?page={CurrentPage}&pageSize={PageSize}";
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            url += $"&query={Uri.EscapeDataString(SearchQuery)}";
        if (SelectedChapterId.HasValue)
            url += $"&chapterId={SelectedChapterId.Value}";

        Response = await Http.GetFromJsonAsync<MemberSearchResponse>(url);

        Loading = false;
        StateHasChanged();
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Blazor/Quartermaster.Blazor.csproj`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Quartermaster.Blazor/Pages/Administration/MemberList.razor Quartermaster.Blazor/Pages/Administration/MemberList.razor.cs
git commit -m "feat: add member list Blazor page with search and chapter filter"
```

---

## Task 9: Blazor Member Detail Page

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/MemberDetail.razor`
- Create: `Quartermaster.Blazor/Pages/Administration/MemberDetail.razor.cs`

- [ ] **Step 1: Create MemberDetail.razor**

Create `Quartermaster.Blazor/Pages/Administration/MemberDetail.razor`:

```razor
@page "/Administration/Members/{Id:guid}"
@using Quartermaster.Api.Members
@using System.Globalization

<div class="mb-3">
    <a href="/Administration/Members" class="btn btn-sm btn-outline-secondary">
        <i class="bi bi-arrow-left"></i> Zurück zur Übersicht
    </a>
</div>

@if (Loading) {
    <div class="d-flex justify-content-center my-4">
        <div class="spinner-border" role="status"></div>
    </div>
} else if (Member == null) {
    <div class="alert alert-danger">Mitglied nicht gefunden.</div>
} else {
    <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
            <h3>@Member.FirstName @Member.LastName</h3>
            <span class="text-secondary">Mitgliedsnummer: @Member.MemberNumber</span>
        </div>
        @if (Member.ExitDate != null) {
            <span class="badge border border-danger text-danger-emphasis fs-6">Ausgetreten</span>
        } else if (Member.IsPending) {
            <span class="badge border border-warning text-warning-emphasis fs-6">Schwebend</span>
        } else {
            <span class="badge border border-success text-success-emphasis fs-6">Aktiv</span>
        }
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>Persönliche Daten</h5>
            <table class="table table-borderless mb-0">
                <tbody>
                    <tr><th style="width:200px">Vorname</th><td>@Member.FirstName</td></tr>
                    <tr><th>Nachname</th><td>@Member.LastName</td></tr>
                    <tr><th>Geburtsdatum</th><td>@(Member.DateOfBirth?.ToString("dd.MM.yyyy") ?? "—")</td></tr>
                    <tr><th>Staatsangehörigkeit</th><td>@(Member.Citizenship ?? "—")</td></tr>
                    <tr><th>E-Mail</th><td>@(Member.EMail ?? "—")</td></tr>
                    <tr><th>Telefon</th><td>@(Member.Phone ?? "—")</td></tr>
                </tbody>
            </table>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>Adresse</h5>
            <table class="table table-borderless mb-0">
                <tbody>
                    <tr><th style="width:200px">Straße</th><td>@(Member.Street ?? "—")</td></tr>
                    <tr><th>PLZ / Ort</th><td>@Member.PostCode @Member.City</td></tr>
                    <tr><th>Land</th><td>@(Member.Country ?? "—")</td></tr>
                    <tr><th>Verwaltungsbezirk</th><td>@(string.IsNullOrEmpty(Member.ResidenceAdministrativeDivisionName) ? "—" : Member.ResidenceAdministrativeDivisionName)</td></tr>
                </tbody>
            </table>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>Gliederung</h5>
            <table class="table table-borderless mb-0">
                <tbody>
                    <tr><th style="width:200px">Gliederung</th><td>@(string.IsNullOrEmpty(Member.ChapterName) ? "—" : Member.ChapterName)</td></tr>
                    <tr><th>Bundesland (CSV)</th><td>@(Member.FederalState ?? "—")</td></tr>
                    <tr><th>Landkreis (CSV)</th><td>@(Member.County ?? "—")</td></tr>
                    <tr><th>Gemeinde (CSV)</th><td>@(Member.Municipality ?? "—")</td></tr>
                </tbody>
            </table>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>Mitgliedschaft</h5>
            <table class="table table-borderless mb-0">
                <tbody>
                    <tr><th style="width:200px">Eintrittsdatum</th><td>@(Member.EntryDate?.ToString("dd.MM.yyyy") ?? "—")</td></tr>
                    <tr><th>Austrittsdatum</th><td>@(Member.ExitDate?.ToString("dd.MM.yyyy") ?? "—")</td></tr>
                    <tr><th>Aufnahmereferenz</th><td>@(Member.AdmissionReference ?? "—")</td></tr>
                    <tr><th>Beitrag</th><td>@Member.MembershipFee.ToString("C2", CultureInfo.GetCultureInfo("de-de"))</td></tr>
                    <tr><th>Geminderter Beitrag</th><td>@Member.ReducedFee.ToString("C2", CultureInfo.GetCultureInfo("de-de"))</td></tr>
                    @if (Member.FirstFee.HasValue) {
                        <tr><th>Erstbeitrag</th><td>@Member.FirstFee.Value.ToString("C2", CultureInfo.GetCultureInfo("de-de"))</td></tr>
                    }
                    @if (Member.ReducedFeeEnd.HasValue) {
                        <tr><th>Minderung bis</th><td>@Member.ReducedFeeEnd.Value.ToString("dd.MM.yyyy")</td></tr>
                    }
                    @if (Member.OpenFeeTotal.HasValue) {
                        <tr><th>Offene Beiträge</th><td>@Member.OpenFeeTotal.Value.ToString("C2", CultureInfo.GetCultureInfo("de-de"))</td></tr>
                    }
                </tbody>
            </table>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>Präferenzen</h5>
            <table class="table table-borderless mb-0">
                <tbody>
                    <tr><th style="width:200px">Stimmrecht</th><td>@BoolIcon(Member.HasVotingRights)</td></tr>
                    <tr><th>Umfragen</th><td>@BoolIcon(Member.ReceivesSurveys)</td></tr>
                    <tr><th>Aktionen</th><td>@BoolIcon(Member.ReceivesActions)</td></tr>
                    <tr><th>Newsletter</th><td>@BoolIcon(Member.ReceivesNewsletter)</td></tr>
                    <tr><th>Post-Rückläufer</th><td>@BoolIcon(Member.PostBounce)</td></tr>
                </tbody>
            </table>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>System</h5>
            <table class="table table-borderless mb-0">
                <tbody>
                    <tr><th style="width:200px">Verknüpfter Benutzer</th><td>@(Member.UserId.HasValue ? Member.UserId.ToString() : "Nicht verknüpft")</td></tr>
                    <tr><th>Letzter Import</th><td>@Member.LastImportedAt.ToString("dd.MM.yyyy HH:mm")</td></tr>
                </tbody>
            </table>
        </div>
    </div>

    <div class="card mb-3">
        <div class="card-body">
            <h5>Audit Log</h5>
            <p class="text-secondary mb-0">Wird in Kürze verfügbar sein.</p>
        </div>
    </div>
}
```

- [ ] **Step 2: Create MemberDetail.razor.cs**

Create `Quartermaster.Blazor/Pages/Administration/MemberDetail.razor.cs`:

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Members;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MemberDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MemberDetailDTO? Member;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            Member = await Http.GetFromJsonAsync<MemberDetailDTO>($"/api/members/{Id}");
        } catch (HttpRequestException) { }

        Loading = false;
    }

    private static string BoolIcon(bool value) => value
        ? "\u2705"
        : "\u274c";
}
```

**Note:** The `BoolIcon` method uses Unicode checkmarks. However, per project rules no emojis unless requested. Use Bootstrap icons instead in the razor template. Replace the `BoolIcon` calls in the razor with inline markup:

Actually, looking at the existing codebase pattern in `MembershipApplicationDetail.razor` (lines 104-108), it uses `<i class="bi bi-check-circle-fill text-success">` and `<i class="bi bi-x-circle-fill text-danger">`. Follow that pattern instead.

Update the razor: replace all `@BoolIcon(...)` calls with inline if/else using the BI icon pattern. And remove `BoolIcon` from the code-behind.

In the razor, replace each `@BoolIcon(Member.HasVotingRights)` etc. with:

```razor
@if (Member.HasVotingRights) {
    <i class="bi bi-check-circle-fill text-success"></i>
} else {
    <i class="bi bi-x-circle-fill text-danger"></i>
}
```

Apply this pattern for all 5 boolean preference fields.

- [ ] **Step 3: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Blazor/Quartermaster.Blazor.csproj`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Quartermaster.Blazor/Pages/Administration/MemberDetail.razor Quartermaster.Blazor/Pages/Administration/MemberDetail.razor.cs
git commit -m "feat: add member detail Blazor page with all data sections"
```

---

## Task 10: Blazor Import History Page

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/MemberImportHistory.razor`
- Create: `Quartermaster.Blazor/Pages/Administration/MemberImportHistory.razor.cs`

- [ ] **Step 1: Create MemberImportHistory.razor**

Create `Quartermaster.Blazor/Pages/Administration/MemberImportHistory.razor`:

```razor
@page "/Administration/Members/Import"
@using Quartermaster.Api.Members

<div class="mb-3">
    <a href="/Administration/Members" class="btn btn-sm btn-outline-secondary">
        <i class="bi bi-arrow-left"></i> Zurück zur Mitgliederliste
    </a>
</div>

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3>Importverlauf</h3>
    <button class="btn btn-primary" @onclick="TriggerImport" disabled="@Importing">
        @if (Importing) {
            <span class="spinner-border spinner-border-sm" role="status"></span>
            <span> Importiere...</span>
        } else {
            <i class="bi bi-download"></i>
            <span> Manueller Import</span>
        }
    </button>
</div>

@if (ImportResult != null) {
    <div class="alert alert-info alert-dismissible mb-3">
        <button type="button" class="btn-close" @onclick="() => ImportResult = null"></button>
        Import abgeschlossen: @ImportResult.TotalRecords Datensätze
        (@ImportResult.NewRecords neu, @ImportResult.UpdatedRecords aktualisiert, @ImportResult.ErrorCount Fehler)
        in @ImportResult.DurationMs ms.
    </div>
}

@if (Loading) {
    <div class="d-flex justify-content-center my-4">
        <div class="spinner-border" role="status"></div>
    </div>
} else if (Response != null) {
    @if (Response.Items.Count == 0) {
        <p class="text-secondary">Noch keine Importe durchgeführt.</p>
    } else {
        <table class="table table-striped table-hover">
            <thead>
                <tr>
                    <th>Zeitpunkt</th>
                    <th>Datei</th>
                    <th>Dauer</th>
                    <th>Gesamt</th>
                    <th>Neu</th>
                    <th>Aktualisiert</th>
                    <th>Fehler</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var log in Response.Items) {
                    <tr>
                        <td>@log.ImportedAt.ToString("dd.MM.yyyy HH:mm")</td>
                        <td>@log.FileName</td>
                        <td>@FormatDuration(log.DurationMs)</td>
                        <td>@log.TotalRecords</td>
                        <td>@log.NewRecords</td>
                        <td>@log.UpdatedRecords</td>
                        <td>
                            @if (log.ErrorCount > 0) {
                                <button class="badge border border-danger text-danger-emphasis btn p-1"
                                        @onclick="() => ToggleErrors(log.Id)">
                                    @log.ErrorCount
                                </button>
                            } else {
                                <span class="badge border border-success text-success-emphasis">0</span>
                            }
                        </td>
                    </tr>
                    @if (ExpandedLogId == log.Id && log.Errors != null) {
                        <tr>
                            <td colspan="7">
                                <div class="bg-body-secondary p-2 rounded small font-monospace">
                                    @foreach (var error in ParseErrors(log.Errors)) {
                                        <div>@error</div>
                                    }
                                </div>
                            </td>
                        </tr>
                    }
                }
            </tbody>
        </table>

        <Pagination CurrentPage="CurrentPage" TotalPages="TotalPages" OnPageChanged="GoToPage" />
    }
}
```

- [ ] **Step 2: Create MemberImportHistory.razor.cs**

Create `Quartermaster.Blazor/Pages/Administration/MemberImportHistory.razor.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Members;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MemberImportHistory {
    [Inject]
    public required HttpClient Http { get; set; }

    private MemberImportLogListResponse? Response;
    private MemberImportLogDTO? ImportResult;
    private bool Loading = true;
    private bool Importing;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private Guid? ExpandedLogId;

    private int TotalPages => Response == null ? 0
        : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
        await LoadHistory();
    }

    private async Task LoadHistory() {
        Loading = true;
        StateHasChanged();

        Response = await Http.GetFromJsonAsync<MemberImportLogListResponse>(
            $"/api/members/import/history?page={CurrentPage}&pageSize={PageSize}");

        Loading = false;
        StateHasChanged();
    }

    private async Task GoToPage(int selectedPage) {
        CurrentPage = selectedPage;
        await LoadHistory();
    }

    private async Task TriggerImport() {
        Importing = true;
        StateHasChanged();

        try {
            var response = await Http.PostAsync("/api/members/import", null);
            if (response.IsSuccessStatusCode) {
                ImportResult = await response.Content.ReadFromJsonAsync<MemberImportLogDTO>();
                CurrentPage = 1;
                await LoadHistory();
            }
        } catch (Exception) {
            // Import failed — reload history to see if partial log was created
            await LoadHistory();
        }

        Importing = false;
        StateHasChanged();
    }

    private void ToggleErrors(Guid logId) {
        ExpandedLogId = ExpandedLogId == logId ? null : logId;
    }

    private static List<string> ParseErrors(string errorsJson) {
        try {
            return JsonSerializer.Deserialize<List<string>>(errorsJson) ?? new();
        } catch {
            return new List<string> { errorsJson };
        }
    }

    private static string FormatDuration(long ms) {
        if (ms < 1000)
            return $"{ms}ms";
        return $"{ms / 1000.0:F1}s";
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Blazor/Quartermaster.Blazor.csproj`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Quartermaster.Blazor/Pages/Administration/MemberImportHistory.razor Quartermaster.Blazor/Pages/Administration/MemberImportHistory.razor.cs
git commit -m "feat: add import history Blazor page with manual trigger and error display"
```

---

## Task 11: Navigation and Final Integration

**Files:**
- Modify: `Quartermaster.Blazor/Layout/MainLayout.razor`

- [ ] **Step 1: Add Mitglieder link to navigation**

In `Quartermaster.Blazor/Layout/MainLayout.razor`, find the "Vorstandsarbeit" dropdown section. Add a new entry after the existing items inside the `<DropdownContent>`:

After the line `<li><a class="dropdown-item" href="/Administration/DueSelections">Beitragseinstufungen</a></li>`, add:

```razor
<li><hr class="dropdown-divider"></li>
<li><a class="dropdown-item" href="/Administration/Members">Mitglieder</a></li>
```

- [ ] **Step 2: Build the full solution**

Run: `dotnet build /media/SMB/Quartermaster/Quartermaster.Server/Quartermaster.Server.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Quartermaster.Blazor/Layout/MainLayout.razor
git commit -m "feat: add Mitglieder link to Vorstandsarbeit navigation dropdown"
```

---

## Task 12: End-to-End Test

- [ ] **Step 1: Configure the import file path**

Start the server. Navigate to `/Administration/Options`, find `member_import.file_path`, and set it to `/media/SMB/sampledata/system_export_testdata.csv`.

- [ ] **Step 2: Test manual import**

Navigate to `/Administration/Members/Import`. Click "Manueller Import". Verify:
- Import completes with 1 total record, 1 new, 0 updated, 0 errors
- Import log appears in the history table

- [ ] **Step 3: Test member list**

Navigate to `/Administration/Members`. Verify:
- One member appears: Joscha Germerott, member number 47125
- City shows "Sibbesse", PLZ "31079"
- Chapter resolved from `USER_LV=NI` (should show Niedersachsen or a sub-chapter)
- Status badge shows "Aktiv" (no exit date, not pending)

- [ ] **Step 4: Test member detail**

Click on the member name. Verify all sections:
- Personal data: name, DOB 16.04.1997, citizenship DE, email
- Address: street, PLZ/city, resolved administrative division
- Chapter: resolved chapter name, FederalState=NI
- Membership: entry date 05.09.2022, fee 72.00, first fee 24.00
- Preferences: all false except as shown
- System: not linked, last import timestamp
- Audit log: "Wird in Kürze verfügbar sein."

- [ ] **Step 5: Test re-import (idempotent)**

Go back to import history. Click "Manueller Import" again. Verify:
- 1 total, 0 new, 1 updated, 0 errors (same member updated)

- [ ] **Step 6: Commit (if any fixes needed)**

Only commit if fixes were needed during testing.
