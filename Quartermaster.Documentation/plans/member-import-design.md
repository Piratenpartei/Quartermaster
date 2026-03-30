# Member Import System — Design Spec

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Import member data from a daily CSV export of the member management software, automatically resolve chapter assignments, and provide admin UI for viewing members and import history.

**Architecture:** Hosted background service polls a configured file path, hashes the file to detect changes, parses with CsvHelper, resolves chapters via ExternalCode hierarchy, and upserts members by member number. Admin pages provide list/detail views and manual import trigger.

**Tech Stack:** CsvHelper (MIT), ASP.NET Core BackgroundService, LinqToDB, Blazor WASM, existing options system for configuration.

---

## 1. Data Model

### 1.1 Member Entity

New entity `Member` in `Quartermaster.Data/Members/Member.cs`:

| Field | Type | Source CSV Column | Notes |
|-------|------|-------------------|-------|
| Id | Guid | — | PK, generated |
| MemberNumber | int | USER_Mitgliedsnummer | Unique, upsert key |
| AdmissionReference | string? | USER_refAufnahme | e.g. "NI-??-3" |
| FirstName | string | Name2 | |
| LastName | string | Name1 | |
| Street | string? | LieferStrasse | |
| Country | string? | LieferLand | |
| PostCode | string? | LieferPLZ | |
| City | string? | LieferOrt | |
| Phone | string? | Telefon | |
| EMail | string? | EMail | |
| DateOfBirth | DateTime? | USER_Geburtsdatum | |
| Citizenship | string? | USER_Staatsbuergerschaft | |
| MembershipFee | decimal | USER_Beitrag | |
| ReducedFee | decimal | USER_redBeitrag | |
| FirstFee | decimal? | USER_Erstbeitrag | |
| ReducedFeeEnd | DateTime? | USER_redBeitragEnde | |
| OpenFeeTotal | decimal? | USER_zoffenerbeitragtotal | |
| EntryDate | DateTime? | USER_Eintrittsdatum | |
| ExitDate | DateTime? | USER_Austrittsdatum | |
| FederalState | string? | USER_Bundesland | Raw CSV value |
| County | string? | USER_Landkreis | Raw CSV value |
| Municipality | string? | USER_Gemeinde | Raw CSV value |
| IsPending | bool | USER_Schwebend | 0/1 in CSV |
| HasVotingRights | bool | USER_zStimmberechtigung | 0/1 in CSV |
| ReceivesSurveys | bool | USER_Umfragen | 0/1 in CSV |
| ReceivesActions | bool | USER_Aktionen | 0/1 in CSV |
| ReceivesNewsletter | bool | USER_Newsletter | 0/1 in CSV |
| PostBounce | bool | USER_Postbounce | 0/1 in CSV |
| ChapterId | Guid? | Resolved | FK to Chapters, resolved from USER_LV/Bezirk/Kreis |
| ResidenceAdministrativeDivisionId | Guid? | Resolved | FK to AdministrativeDivisions, resolved from PLZ |
| UserId | Guid? | — | FK to Users, nullable, for future SSO linking |
| LastImportedAt | DateTime | — | Timestamp of last import that touched this record |

### 1.2 MemberImportLog Entity

New entity `MemberImportLog` in `Quartermaster.Data/Members/MemberImportLog.cs`:

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| ImportedAt | DateTime | When the import ran |
| FileName | string | Source file name |
| FileHash | string | SHA256 of the file |
| TotalRecords | int | Total rows in CSV |
| NewRecords | int | New members inserted |
| UpdatedRecords | int | Existing members updated |
| ErrorCount | int | Rows that failed |
| Errors | string? | JSON array of error messages |
| DurationMs | long | Import duration in milliseconds |

### 1.3 Chapter Changes

Add `ExternalCode` (string?, nullable) column to `Chapter` entity and migration.

External codes represent the raw values from the member management CSV:
- State level: "NW", "BY", "NI", etc. (official 2-char state codes)
- Bezirk level: "RB Arnsberg", "Bezirksverband Oberbayern", "B-Friedrichshain-Kreuzberg", etc.
- Kreis level: "Dortmund", "Kreisverband München-Stadt", "vKV Görlitz", etc.

## 2. Chapter Seeding

Extend `ChapterRepository.SupplementDefaults()` to create Bezirk and Kreis level chapters from the ~220 unique combinations found in the chapter export CSV.

### 2.1 Seeding Rules

- State chapters already exist with ShortCodes (bw, by, nds, etc.). Add ExternalCode to them (BW, BY, NI, etc.).
- Bezirk chapters: created as children of their state chapter. Name = full CSV value (e.g. "Bezirksverband Oberbayern"). ExternalCode = same.
- Kreis chapters: created as children of their Bezirk chapter (or state chapter if Bezirk is NULL). Name = full CSV value. ExternalCode = same.
- AdministrativeDivisionId: attempt to match by name to existing divisions where possible.
- "Ausland" (abroad) state: create a chapter with ExternalCode "Ausland", no AdministrativeDivisionId.

### 2.2 State Code Mapping

| CSV Code | ShortCode | State |
|----------|-----------|-------|
| BW | bw | Baden-Württemberg |
| BY | by | Bayern |
| BE | be | Berlin |
| BB | bb | Brandenburg |
| HB | hb | Bremen |
| HH | hh | Hamburg |
| HE | he | Hessen |
| MV | mv | Mecklenburg-Vorpommern |
| NI | nds | Niedersachsen |
| NW | nrw | Nordrhein-Westfalen |
| RP | rlp | Rheinland-Pfalz |
| SL | sl | Saarland |
| SN | sn | Sachsen |
| ST | st | Sachsen-Anhalt |
| SH | sh | Schleswig-Holstein |
| TH | th | Thüringen |
| Ausland | — | Abroad |

## 3. Import Pipeline

### 3.1 MemberImportService

Singleton service registered in DI. Core method: `ImportFromFile(string filePath) -> MemberImportLog`.

**Process per file:**
1. Read file, compute SHA256 hash
2. Parse CSV with CsvHelper (semicolon delimiter, header row)
3. Pre-load all chapters with ExternalCode into a lookup dictionary
4. For each row:
   a. Parse fields, convert NULLs/empty to null
   b. Resolve chapter: try Kreis ExternalCode (under matching Bezirk/LV parent), then Bezirk, then LV
   c. Resolve ResidenceAdministrativeDivisionId from PLZ via AdministrativeDivisionRepository
   d. Upsert by MemberNumber — insert or update
   e. Track new/updated/error counts
5. Return MemberImportLog with stats

**Error handling:** Individual row failures are collected as errors but do not stop the import. The import log records all errors as a JSON array.

**Audit stub:** Add a TODO comment where change tracking would go. The upsert logic should be structured so that a diff can be computed later (compare old vs new field values before saving).

### 3.2 MemberImportHostedService

ASP.NET Core `BackgroundService` that polls for file changes.

**Loop:**
1. Read `member_import.file_path` from options
2. Read `member_import.polling_interval_minutes` from options (default 10)
3. Check if file exists
4. Compute SHA256 of file
5. Compare to in-memory `_lastFileHash`
6. If unchanged → sleep and repeat
7. If changed → call `MemberImportService.ImportFromFile()`
8. Store new hash in memory
9. Save `MemberImportLog` to database
10. Sleep for polling interval, repeat

**Resilience:** Catch all exceptions in the loop, log them, continue polling. Never crash the hosted service.

### 3.3 New Options

Add to `OptionRepository.SupplementDefaults()`:
- `member_import.file_path` — String, not overridable, default empty string
- `member_import.polling_interval_minutes` — Number, not overridable, default "10"

## 4. CSV Parsing

**Format:** Semicolon-delimited, first row is header, encoding likely Windows-1252 or UTF-8.

**Field mapping with CsvHelper ClassMap:**
- `Adresse` → skip (not stored)
- `USER_Mitgliedsnummer` → MemberNumber (int)
- `USER_refAufnahme` → AdmissionReference (string?)
- `Name1` → LastName
- `Name2` → FirstName
- `LieferStrasse` → Street
- `LieferLand` → Country
- `LieferPLZ` → PostCode
- `LieferOrt` → City
- `Telefon` → Phone
- `EMail` → EMail
- `USER_LV` → used for chapter resolution + stored in FederalState
- `USER_Bezirk` → used for chapter resolution (not stored separately)
- `USER_Kreis` → used for chapter resolution (not stored separately)
- `USER_Beitrag` → MembershipFee (decimal)
- `USER_redBeitrag` → ReducedFee (decimal)
- `USER_Geburtsdatum` → DateOfBirth (DateTime?)
- `USER_Bundesland` → FederalState (string?)
- `USER_Eintrittsdatum` → EntryDate (DateTime?)
- `USER_Austrittsdatum` → ExitDate (DateTime?)
- `USER_Erstbeitrag` → FirstFee (decimal?)
- `USER_Landkreis` → County (string?)
- `USER_Gemeinde` → Municipality (string?)
- `USER_Staatsbuergerschaft` → Citizenship (string?)
- `USER_Schwebend` → IsPending (0/1 → bool)
- `USER_zStimmberechtigung` → HasVotingRights (0/1 → bool)
- `USER_zoffenerbeitragtotal` → OpenFeeTotal (decimal?)
- `USER_redBeitragEnde` → ReducedFeeEnd (DateTime?)
- `USER_Umfragen` → ReceivesSurveys (0/1 → bool)
- `USER_Aktionen` → ReceivesActions (0/1 → bool)
- `USER_Newsletter` → ReceivesNewsletter (0/1 → bool)
- `USER_Postbounce` → PostBounce (0/1 → bool)

**Skipped CSV fields:**
- `Adresse` — external address ID, not needed
- `USER_Wahlkreis`, `USER_Stimmkreis` — electoral districts, not used currently
- `USER_OV` — Ortsverband, below Kreis level, not modeled as chapters yet
- `USER_WahlkreisBTW`, `USER_WahlkreisLTW` — federal/state electoral districts
- `USER_BeitragBis2011` — financial data past retention period

## 5. Chapter Resolution During Import

For each member row, resolve the most specific chapter:

```
function ResolveChapter(lv, bezirk, kreis, chapterLookup):
    if kreis is not NULL:
        find chapter where ExternalCode == kreis
            AND parent.ExternalCode == bezirk (or parent is LV if bezirk is NULL)
            AND grandparent/parent ExternalCode matches lv
        if found → return chapter

    if bezirk is not NULL:
        find chapter where ExternalCode == bezirk
            AND parent.ExternalCode == lv
        if found → return chapter

    if lv is not NULL and lv != "Ausland":
        find chapter where ExternalCode == lv AND parent is Bundesverband
        if found → return chapter

    if lv == "Ausland":
        find chapter where ExternalCode == "Ausland"
        if found → return chapter

    return null
```

The lookup is pre-loaded at import start (all chapters with their ExternalCode and parent chain) to avoid per-row database queries.

## 6. Residence Resolution

For each member row, resolve the administrative division from postal code:
1. Use `AdministrativeDivisionRepository.Search()` with the member's `LieferPLZ`
2. If exactly one result → use it
3. If multiple results → pick the one matching `LieferOrt` (city) if possible, otherwise pick the most specific (deepest depth)
4. If no results → leave null

## 7. API Endpoints & DTOs

### 7.1 DTOs

**MemberDTO** (list view):
- Id, MemberNumber, FirstName, LastName, PostCode, City, ChapterId, ChapterName, EntryDate, ExitDate, IsPending, HasVotingRights

**MemberDetailDTO** (detail view):
- All Member entity fields, plus ChapterName, ResidenceAdministrativeDivisionName

**MemberSearchRequest:**
- Query (string?), ChapterId (Guid?), Page (int), PageSize (int)

**MemberSearchResponse:**
- Items (List<MemberDTO>), TotalCount (int)

**MemberImportLogDTO:**
- All MemberImportLog entity fields

**MemberImportLogListResponse:**
- Items (List<MemberImportLogDTO>), TotalCount (int)

### 7.2 Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/members | Paginated member list, searchable by name/number, filterable by chapter |
| GET | /api/members/{id} | Full member detail |
| POST | /api/members/import | Manual import trigger, returns MemberImportLogDTO |
| GET | /api/members/import/history | Paginated import history |

## 8. Admin UI

### 8.1 Member List (`/Administration/Members`)

- Paginated table: Member Number, Name, City, Chapter, Entry Date, Status
- Search bar filtering by name or member number
- Chapter dropdown filter
- Shared Pagination component
- Status badges (border-only style):
  - Active (has entry date, no exit date, not pending) → green
  - Exited (has exit date) → red
  - Pending (IsPending) → yellow
- Rows link to detail page
- Header area: link to import history page

### 8.2 Member Detail (`/Administration/Members/{Id}`)

- Back button to list
- Header: full name + member number + status badge
- Card sections:
  - **Personal Data:** name, DOB, citizenship, email, phone
  - **Address:** street, PLZ, city, country, resolved administrative division
  - **Chapter:** assigned chapter name, federal state / county / municipality from CSV
  - **Membership:** entry date, exit date, admission reference, fees (current, reduced, first), reduced fee end, open fee total
  - **Preferences:** surveys, actions, newsletter, post bounce, voting rights
  - **System:** linked user (shows "not linked" for now), last imported at
  - **Audit Log:** placeholder — "Coming soon" message

### 8.3 Import History (`/Administration/Members/Import`)

- Manual import trigger button (with loading state)
- Paginated table: timestamp, filename, duration, total/new/updated/error count
- Error count as red badge; expandable to show error details
- Shared Pagination component

### 8.4 Navigation

Add "Mitglieder" link under the "Vorstandsarbeit" dropdown in MainLayout.

## 9. Migration Changes

All in M001 (project is unreleased):
- Add `ExternalCode` column to Chapters table (nullable string)
- Create Members table with all fields and FKs
- Create MemberImportLogs table
