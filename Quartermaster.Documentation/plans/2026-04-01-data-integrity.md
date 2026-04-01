# Data Integrity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add database indexes for common queries, CASCADE DELETE for safe parent-child relationships, soft-delete for major entities (except Members), and fix the unbounded member query.

**Architecture:** A new FluentMigrator migration (M002) adds indexes, alters FK cascade rules, and adds `DeletedAt` columns. Entity classes get `DeletedAt` nullable DateTime. Repository queries filter `WHERE DeletedAt IS NULL`. Members remain hard-deletable per legal requirements.

**Tech Stack:** FluentMigrator, LinqToDB, existing repository pattern

---

## Decisions

**Soft-delete entities** (add `DeletedAt` column): Event, Motion, MembershipApplication, DueSelection, User, EventTemplate

**Hard-delete only** (no soft-delete): Member (legal requirement — must be fully removed), EventChecklistItem, ChapterOfficer, MotionVote, SystemOption, Token

**CASCADE DELETE FKs:**
- MotionVotes.MotionId → Motions.Id
- EventChecklistItems.EventId → Events.Id
- SystemOptions.ChapterId → Chapters.Id
- Tokens.UserId → Users.Id

**Indexes to add:**
- Member(MemberNumber) — already unique, verify indexed
- Member(LastName, FirstName) — name search
- Event(ChapterId) — events by chapter
- Motion(ChapterId) — motions by chapter
- Chapter(ShortCode) — chapter lookup
- Chapter(ExternalCode) — member import resolution
- EventChecklistItem(EventId) — checklist by event
- MotionVote(MotionId) — votes by motion
- ChapterOfficer(ChapterId) — officers by chapter
- Token(UserId) — tokens by user

---

## Tasks

### Task 1: Create M002 migration with indexes, cascades, and soft-delete columns

**Files:**
- Create: `Quartermaster.Data/Migrations/M002_DataIntegrityMigration.cs`

Create the migration that:
1. Adds `DeletedAt` (nullable DateTime) column to: Events, Motions, MembershipApplications, DueSelections, Users, EventTemplates
2. Adds secondary indexes
3. Drops existing FKs and recreates with CASCADE DELETE for the 4 relationships

The migration must extend `MigrationBase` (which has `DropTableIfExists`, `DisableForeignKeyChecks`, `EnableForeignKeyChecks` helpers).

Table names are constants on the entity classes (e.g., `Event.TableName`, `Motion.TableName`).

Entity table name constants to reference:
- `User.TableName`, `Event.TableName`, `Motion.TableName`, `EventTemplate.TableName`
- `MembershipApplication.TableName`, `DueSelection.TableName`
- `Member.TableName`, `Chapter.TableName`, `EventChecklistItem.TableName`
- `MotionVote.TableName`, `Token.TableName`, `SystemOption.TableName`
- `ChapterOfficer.TableName` (which is `"ChapterAssociates"`)

FK names to drop and recreate (from M001):
- `FK_MotionVotes_MotionId_Motions_Id` → add CASCADE
- `FK_EventChecklistItems_EventId_Events_Id` → add CASCADE
- `FK_SystemOptions_ChapterId_Chapters_Id` → add CASCADE
- `FK_Tokens_UserId_User_Id` → add CASCADE

### Task 2: Add DeletedAt to entity classes and update repositories

**Entities to modify** (add `public DateTime? DeletedAt { get; set; }`):
- `Quartermaster.Data/Events/Event.cs`
- `Quartermaster.Data/Motions/Motion.cs`
- `Quartermaster.Data/MembershipApplications/MembershipApplication.cs`
- `Quartermaster.Data/DueSelector/DueSelection.cs`
- `Quartermaster.Data/Users/User.cs`
- `Quartermaster.Data/Events/EventTemplate.cs`

**Repositories to modify** (add `.Where(x => x.DeletedAt == null)` to all read queries):
- `Quartermaster.Data/Events/EventRepository.cs` — event list, detail, template list, template detail
- `Quartermaster.Data/Motions/MotionRepository.cs` — motion list, detail
- `Quartermaster.Data/MembershipApplications/MembershipApplicationRepository.cs` — list, detail
- `Quartermaster.Data/DueSelector/DueSelectionRepository.cs` — list, detail
- `Quartermaster.Data/Users/UserRepository.cs` — user queries

**Add soft-delete methods** to repositories for soft-deletable entities:
```csharp
public void SoftDelete(Guid id) {
    _context.TableName.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
}
```

### Task 3: Fix unbounded query in MemberEmailService

**File:** `Quartermaster.Server/Events/MemberEmailService.cs`

Replace `pageSize: 100000` with batched iteration capped at 500 per page.

### Task 4: Drop DB, rebuild, verify

Drop database, restart server, generate test data, run tests, verify in Chrome.

---

## Checklist

| TODO | Status |
|---|---|
| Review FK cascade behavior | ✅ 4 FKs get CASCADE DELETE |
| Add proper deletion logic: soft-delete vs cascade | ✅ Soft-delete for 6 entity types; hard-delete for leaf records |
| Fix unbounded member query in MemberEmailService | ✅ Capped at 500 with pagination |
| Add database indexes for common query patterns | ✅ 10+ secondary indexes added |
