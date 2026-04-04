# Administrative Division Background Service — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move admin division loading from blocking startup to a background service with change detection, intelligent remapping of removed divisions, and detailed logging.

**Architecture:** A new `AdminDivisionImportService` handles file parsing and DB comparison. An `AdminDivisionImportHostedService` runs it once on startup (non-blocking) then daily. Change detection compares loaded file data against existing DB records. Removed divisions are remapped by postcode match → parent division → orphan retention. All changes and failures are logged to a new `AdminDivisionImportLog` table.

**Tech Stack:** Existing BackgroundService pattern (like MemberImportHostedService), SHA256 file hashing for change detection, LinqToDB for DB operations.

---

### Task 1: Remove AdminDivs from synchronous startup

**Files:**
- Modify: `Quartermaster.Data/DbContext.cs`
- Modify: `Quartermaster.Data/AdministrativeDivisions/AdministrativeDivisionRepository.cs`

- [ ] **Step 1:** In `DbContext.SupplementDefaults()`, change `SupplementDefaults(true)` to `SupplementDefaults(false)` — the background service will handle file loading instead.

```csharp
// Before:
scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>().SupplementDefaults(true);
// After:
scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>().SupplementDefaults(false);
```

- [ ] **Step 2:** Build and verify startup is fast.

---

### Task 2: Create AdminDivisionImportLog entity and migration

**Files:**
- Create: `Quartermaster.Data/AdministrativeDivisions/AdminDivisionImportLog.cs`
- Modify: `Quartermaster.Data/Migrations/M001_InitialStructureMigration.cs`
- Modify: `Quartermaster.Data/DbContext.cs` (add ITable)

- [ ] **Step 1:** Create entity:

```csharp
namespace Quartermaster.Data.AdministrativeDivisions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AdminDivisionImportLog {
    public const string TableName = "AdminDivisionImportLogs";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ImportedAt { get; set; }
    public string FileHash { get; set; } = "";
    public int TotalRecords { get; set; }
    public int AddedRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int RemovedRecords { get; set; }
    public int RemappedRecords { get; set; }
    public int OrphanedRecords { get; set; }
    public int ErrorCount { get; set; }
    public string? Errors { get; set; }
    public long DurationMs { get; set; }
}
```

- [ ] **Step 2:** Add table to M001 migration and DbContext.

---

### Task 3: Create AdminDivisionImportService with change detection

**Files:**
- Create: `Quartermaster.Server/AdministrativeDivisions/AdminDivisionImportService.cs`

This is the core service. It:
1. Parses files using the existing `AdministrativeDivisionLoader` logic (refactored to return data without inserting)
2. Computes combined file hash for change detection
3. Compares loaded data against DB:
   - **New divisions:** Insert
   - **Changed divisions (name or postcodes changed, identifiable by the other):** Update in place
   - **Removed divisions:** Try remapping:
     a. Find new division that has the same postcode → remap members/chapters
     b. If no postcode match, find parent division → remap to parent
     c. If neither works, mark as orphan (keep in DB, log error for admin review)
4. Updates `Members.ResidenceAdministrativeDivisionId` and `Chapters.AdministrativeDivisionId` for remapped divisions
5. Logs everything to `AdminDivisionImportLog`

**Key matching logic:**
- Each admin division has a unique `AdminCode` (compound key from the source data like `DE.NI.03254.03254028`)
- Identity: matched by `AdminCode`
- Change detection: compare `Name` and postcodes
- If only name changed but AdminCode is the same → update name
- If AdminCode is removed but its postcodes appear in a new AdminCode → remap to new division
- If AdminCode is removed and postcodes don't match anywhere → try parent (one level up in AdminCode hierarchy)
- If parent doesn't exist either → orphan

---

### Task 4: Refactor AdministrativeDivisionLoader to return data without inserting

**Files:**
- Modify: `Quartermaster.Data/AdministrativeDivisions/AdministrativeDivisionLoader.cs`

- [ ] **Step 1:** Extract the file parsing logic into a method that returns the list of `AdministrativeDivision` objects without inserting them. Keep the existing `Load()` method as a convenience wrapper that calls parse + insert.

```csharp
public static List<AdministrativeDivision> Parse(string baseFilePath, string postcodeFilePath) {
    // Existing parse logic from Load(), but returns bulkData list instead of calling CreateBulk
}

public static void Load(string baseFilePath, string postcodeFilePath,
    AdministrativeDivisionRepository adminDivRepo) {
    var data = Parse(baseFilePath, postcodeFilePath);
    adminDivRepo.CreateBulk(data);
}
```

---

### Task 5: Create AdminDivisionImportHostedService

**Files:**
- Create: `Quartermaster.Server/AdministrativeDivisions/AdminDivisionImportHostedService.cs`
- Modify: `Quartermaster.Server/Program.cs` (register services)

- [ ] **Step 1:** Create hosted service following MemberImportHostedService pattern:
- Runs once 5 seconds after startup (non-blocking)
- Then polls daily
- Uses file hash to skip if files haven't changed
- Calls `AdminDivisionImportService.Import()`

- [ ] **Step 2:** Register in Program.cs:
```csharp
builder.Services.AddSingleton<AdminDivisionImportService>();
builder.Services.AddHostedService<AdminDivisionImportHostedService>();
```

---

### Task 6: Add manual trigger endpoint and admin status endpoint

**Files:**
- Create: `Quartermaster.Server/AdministrativeDivisions/AdminDivisionImportTriggerEndpoint.cs`
- Create: `Quartermaster.Server/AdministrativeDivisions/AdminDivisionImportHistoryEndpoint.cs`

- [ ] **Step 1:** Trigger endpoint (POST `/api/admin/admindivisions/import`) — requires `TriggerMemberImport` permission (reuse existing permission), calls `AdminDivisionImportService.Import()`, returns log.

- [ ] **Step 2:** History endpoint (GET `/api/admin/admindivisions/importlogs`) — returns recent import logs for admin review.

---

### Task 7: Add TODO for background service status UI

Add to production-readiness-todos.md:
- Admin page to view import service statuses (member import + admin division import)
- Show last run, current status, trigger buttons
- Display error details from import logs

---

### Task 8: Build, test, verify

- [ ] Build and run tests
- [ ] Restart server — verify startup is fast (no AdminDivs loading)
- [ ] Verify admin divisions load in background (check logs)
- [ ] Verify manual trigger works via API
