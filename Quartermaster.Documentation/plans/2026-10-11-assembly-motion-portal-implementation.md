# Assembly Motion Portal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Antragsportal: members write and co-submit motions for party assemblies in Quartermaster, the motion commission processes them, every submitted motion is mirrored into the Piratenwiki and can be pushed into OpenSlides.

**Architecture:** New `AssemblyMotions` folders in Api (enums, DTOs, requests), Data (entities, repositories, migration `M004`), Server (domain services, FastEndpoints endpoints, background services, wiki and OpenSlides clients), Rendering (Markdown to wikitext) and Blazor (public, member and commission pages). All lifecycle rules live in server services that take the current time as a parameter, so they are unit-testable against the per-worker test database. External systems (wiki, OpenSlides) sit behind interfaces with fakes in tests.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator on MariaDB, Blazor WASM, Markdig, Fluid, TUnit.

**Spec:** `Quartermaster.Documentation/plans/2026-10-11-assembly-motion-portal-design.md`

## Global Constraints

- The plan has five phases. Each phase ends with a checkpoint task: build, run that phase's test classes, restart the local server, stop and hand over to the user. Never start the next phase before the user says so.
- Code style: everything in `CLAUDE.md` applies (K&R braces, if-body on the next line, explicit access modifiers, `var`, fluent LINQ only, one class per file except DTO files, no `@code` in `.razor`, no `using` aliases, no region comments, `System.Threading.Lock`, mapping methods on the types).
- Comments: none by default. A `///` summary only for a member contract that is not obvious; a WHY note goes into a `//` comment at the code.
- Never run any git command. The user commits.
- Never run the full test suite. Build with `cd /media/SMB/Quartermaster && dotnet build Quartermaster.sln -m:2 -v:q -nologo`, run only the classes named in the task with `dotnet run --no-build --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj -- --treenode-filter "/*/*/<ClassName>/*"`.
- Schema: the whole data model of all phases is created in Task 1 in the new migration `Quartermaster.Data/Migrations/M004.cs` with `[Migration(006)]`. Later phases do not add columns. If a later task finds a missing column, add it to `M004` (V4 is unreleased) and tell the controller, because the developer database then has to be altered by hand.
- Datetime columns `.AsCustom("DATETIME(6)")`, Markdown/HTML/template columns `.AsCustom("MEDIUMTEXT")`, Guids `.AsGuid()`, enums `.AsInt32()`. Timestamps are stored in UTC.
- Enums are persisted as int: only ever append values, never reorder.
- Deadlines are entered and displayed in Europe/Berlin through `BerlinTime` (Task 3). DTOs carry UTC.
- User-facing strings are German and go through `I18nKey` with entries in both `Quartermaster.Server/wwwroot/i18n/de.json` and `en.json`. Parameterized messages use `I18nParams.With(key, ("name", value))` with `{name}` placeholders.
- Anonymous endpoints return dedicated public DTOs that have no name or member fields at all.
- Motion text and reason are restricted Markdown (`MarkdownFlavor.Motion`, Task 4): CommonMark plus pipe tables and strikethrough, no raw HTML, no images. Every place that renders or previews motion text or reason uses this flavour.
- Wiki and OpenSlides: tests use fakes only. Nothing on the VM may write to the real Piratenwiki or a real OpenSlides instance.
- No em or en dashes anywhere (code strings, docs, changelog); use a plain hyphen.
- Changelog: `Quartermaster.Server/Changelog/V4.md`, German, `### Heading` plus one paragraph per feature, user-visible changes only.
- Terms: add the spec's terms table to `Quartermaster.Documentation/Translations.md` (Task 11).
- Permission: `assemblymotions_manage` is chapter-scoped and does not inherit to sub-chapters (only `_view` permissions inherit). Commission checks use `PermissionContext.Has(event.ChapterId, PermissionIdentifier.ManageAssemblyMotions)`.

## Review Focus

- Two members join a motion at the same moment as the last required submitter: exactly one transition to `Submitted`, one number, no duplicate submitter rows. Pinned in Task 6 (`Concurrent_last_joins_submit_once`).
- A deadline entered as 23:59 Berlin time on a date in summer time and one in winter time: actions at 23:59:30 Berlin are refused, actions at 23:58 are allowed. Pinned in Task 3 (`BerlinTime_round_trips_across_dst`) and Task 6 (`Release_refused_one_minute_after_deadline`).
- Hostile Markdown in a motion (`|`, `}}`, `{{subst:...}}`, `</nowiki>`, `[[Kategorie:...]]`, a raw `<ref>` tag) must not change the template call in the wiki, even if it slipped past the input validation. Pinned in Task 21; the input validation in Task 4.
- An author edits a submitted motion of a type that needs co-submitters: from that moment the portal, the public view and the wiki all show the new text with the status "In Überarbeitung" and no names; nobody sees the old text as the current one. Pinned in Task 9 (`Edited_motion_shows_current_text_in_revision_anonymously`) and Task 24 (`Edited_motion_updates_wiki_to_in_revision`).
- An OpenSlides push that fails halfway: a second push must not create duplicates of the motions that were created in the first run. Pinned in Task 27 (`Repush_after_partial_failure_creates_no_duplicates`).

## File Structure

```
Quartermaster.Api/AssemblyMotions/
  AssemblyMotionStatus.cs, AssemblyMotionResult.cs, RequiredMajority.cs, WikiSyncState.cs, EligibilityProblem.cs   enums
  AssemblyMotionNumber.cs          display number formatting (SÄA001)
  BerlinTime.cs                    Europe/Berlin <-> UTC
  TextDiff.cs                      line diff for revision history (phase 2)
  *DTO.cs, *Request.cs             one file per DTO group / request
Quartermaster.Data/AssemblyMotions/
  AssemblyMotionPortal.cs, AssemblyMotionType.cs, AssemblyMotion.cs, AssemblyMotionSubmitter.cs,
  AssemblyMotionRevision.cs, AssemblyMotionInquiry.cs, AssemblyMotionCompetition.cs, AssemblyMotionUpvote.cs
  AssemblyMotionPortalRepository.cs     portal + types
  AssemblyMotionRepository.cs           motions, submitters, revisions, numbering, row locks
  AssemblyMotionInquiryRepository.cs    (phase 2)
  AssemblyMotionCompetitionRepository.cs, AssemblyMotionUpvoteRepository.cs   (phase 3)
  AssemblyMotionDefaultTypes.cs         default type set
Quartermaster.Data/Migrations/M004.cs
Quartermaster.Rendering/MarkdownFlavor.cs, MotionMarkdown.cs     restricted Markdown for motion texts (phase 1)
Quartermaster.Rendering/MotionWikiConverter.cs                  Markdown -> sanitized HTML -> MediaWiki (phase 4)
Quartermaster.Server/AssemblyMotions/
  AssemblyMotionDeadlines.cs, AssemblyMotionEligibility.cs, AssemblyMotionVisibility.cs, AssemblyMotionAccess.cs
  AssemblyMotionService.cs              member lifecycle
  AssemblyMotionCommissionService.cs    (phase 2)
  AssemblyMotionOutcome.cs              result of a service action
  AssemblyMotionDeadlineHostedService.cs
  Endpoints/Portal/*, Endpoints/Public/*, Endpoints/Member/*, Endpoints/Commission/*
  Notifications/*                       (phase 2)
  Wiki/*                                (phase 4)
  OpenSlides/*                          (phase 5)
Quartermaster.Blazor/Pages/AssemblyMotions/*                    public + member pages
Quartermaster.Blazor/Pages/Administration/AssemblyMotions/*     commission pages
Quartermaster.Blazor/Components/AssemblyMotions/*               badges, portal settings section
Quartermaster.Server.Tests/AssemblyMotions/*                    service + repository tests (RepositoryTestBase)
Quartermaster.Server.Tests/Integration/AssemblyMotions/*        endpoint tests (IntegrationTestBase)
```

Patterns to copy (read these before writing the matching code):

- Entity, `ToDto`: `Quartermaster.Data/Motions/Motion.cs`; table registration `Quartermaster.Data/DbContext.cs:34-79`; repository registration `DbContext.AddRepositories` (line ~83); seeding `DbContext.SupplementDefaults` (line ~120).
- Repository with audit and transactions: `Quartermaster.Data/Motions/MotionRepository.cs` (`using var tx = _context.BeginTransaction(); ... tx.Commit();`).
- Migration: `Quartermaster.Data/Migrations/M003.cs` (tables, FKs, composite unique index with `.WithOptions().Unique()`, `Down()` with `DropTableIfExists`).
- Options: `OptionRepository.SupplementDefaults` with `AddDefinitionIfNotExists(..., isSecret: true)`; settings object read like `Quartermaster.Server/Accreditation/KeycloakSettings.cs`.
- Permission seeding: `PermissionRepository.SupplementDefaults` (`AddIfNotExists(id, displayName, global)`); system role seeding: `RoleRepository.SupplementDefaults` (ChapterOfficer block).
- Endpoint with validator: `Quartermaster.Server/Motions/MotionUpdateEndpoint.cs` + `MotionUpdateRequestValidator.cs`; anonymous: `Quartermaster.Server/Events/EventDetailEndpoint.cs`; paginated: `MotionListEndpoint.cs` with `IPaginatedRequest`/`IPaginatedResponse<T>` and `PaginationValidationExtensions`.
- Outbound HTTP client registration and fake: `Program.cs:156-162` (Keycloak), `Quartermaster.Server/NewsPosts/Programme/HttpWikiClient.cs` (User-Agent handling), `Quartermaster.Server.Tests/Accreditation/Fakes/FakeKeycloakAdminClient.cs` (static fake + `Reset()` + `[NotInParallel]`).
- Background service loop: `Quartermaster.Server/Members/RetentionAnonymizationHostedService.cs`.
- Notifications: `NotificationTriggers.cs`, `MotionSubmittedPayload.cs`, `MotionSubmittedRecipientResolver.cs`, `ChapterPermissionRecipientResolver.cs`, `NotificationTriggerCatalog.cs`, `Quartermaster.Data/Templates/SystemTemplateSeeds.cs`, `Quartermaster.Api/Templates/TemplateModelLookup.cs`, firing in `Quartermaster.Server/Submissions/SubmissionMaterializer.cs:109-120`.
- Tests: `Quartermaster.Server.Tests/Infrastructure/IntegrationTestBase.cs`, `RepositoryTestBase.cs`, `TestDataBuilder.cs` (`SeedChapter`, `SeedChapterHierarchy`, `SeedMember`, `SeedEvent`, `SeedUser`, `SeedAuthenticatedUser`), endpoint test `Integration/Motions/MotionListEndpointTests.cs`, fake swap `Accreditation/AccreditationEndpointTests.cs:19-38`.
- Blazor: admin `Pages/Administration/EventDetail.razor(.cs)`, public `Pages/Events/PublicEventDetail.razor(.cs)`, list `Pages/Administration/MotionList.razor(.cs)`, detail `Pages/Administration/MotionDetail.razor(.cs)`, components `MarkdownEditorWithPreview`, `ConfirmDialog`, `Pagination`, badge `MotionApprovalBadge`, nav `Layout/MainNavBar.razor`.

---

# Phase 1: Portal, Drafting, Co-submitting, Public and Member Views

### Task 1: Schema, enums, entities, permission, role and options

**Files:**
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionStatus.cs`, `AssemblyMotionResult.cs`, `RequiredMajority.cs`, `WikiSyncState.cs`, `EligibilityProblem.cs`
- Create: `Quartermaster.Data/AssemblyMotions/AssemblyMotionPortal.cs`, `AssemblyMotionType.cs`, `AssemblyMotion.cs`, `AssemblyMotionSubmitter.cs`, `AssemblyMotionRevision.cs`, `AssemblyMotionInquiry.cs`, `AssemblyMotionCompetition.cs`, `AssemblyMotionUpvote.cs`
- Create: `Quartermaster.Data/Migrations/M004.cs`
- Modify: `Quartermaster.Data/DbContext.cs` (8 `ITable<>` properties)
- Modify: `Quartermaster.Api/PermissionIdentifier.cs` (new permission + `SystemRole.MotionCommission`)
- Modify: `Quartermaster.Data/Permissions/PermissionRepository.cs`, `Quartermaster.Data/Roles/RoleRepository.cs`, `Quartermaster.Data/Options/OptionRepository.cs` (seeding)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionSeedingTests.cs`

**Interfaces:**
- Produces: the enums, the entities with the exact property names below, table name constants, `PermissionIdentifier.ManageAssemblyMotions = "assemblymotions_manage"`, `PermissionIdentifier.SystemRole.MotionCommission = "motion_commission"`, options `wiki.api_url`, `wiki.bot_username`, `wiki.bot_password`, `wiki.protection_level`, `openslides.url`, `openslides.username`, `openslides.password`.

- [ ] **Step 1: Enums** (namespace `Quartermaster.Api.AssemblyMotions`)

```csharp
public enum AssemblyMotionStatus {
    Draft = 0,
    CollectingCoSubmitters = 1,
    Submitted = 2,
    Admitted = 3,
    FormallyInsufficient = 4,
    Withdrawn = 5,
    Lapsed = 6
}

public enum AssemblyMotionResult {
    None = 0,
    Accepted = 1,
    AcceptedModified = 2,
    Rejected = 3,
    NotHandled = 4
}

public enum RequiredMajority {
    Simple = 0,
    TwoThirds = 1
}

public enum WikiSyncState {
    NotMirrored = 0,
    Pending = 1,
    Synced = 2,
    Failed = 3,
    ProtectionFailed = 4
}

public enum EligibilityProblem {
    None = 0,
    NotSignedIn = 1,
    NoLinkedMember = 2,
    NoMemberNumber = 3,
    Pending = 4,
    Anonymized = 5,
    Exited = 6,
    OutsideChapter = 7
}
```

- [ ] **Step 2: Entities** (namespace `Quartermaster.Data.AssemblyMotions`, `[Table(TableName, IsColumnAttributeRequired = false)]`, `[PrimaryKey] public Guid Id { get; set; } = Guid.NewGuid();`, `public const string TableName = "..."`). Property lists, exactly:

- `AssemblyMotionPortal` (`AssemblyMotionPortals`): `Id`, `EventId`, `OpensAt`, `ClosesAt`, `IntroMarkdown` (string, ""), `IntroHtml` (string, ""), `PrioritizationEnabled`, `PrioritizationStartsAt` (DateTime?), `PrioritizationEndsAt` (DateTime?), `WikiMirrorEnabled`, `WikiPagePrefix` (string?), `WikiAssemblyLabel` (string?), `OpenSlidesMeetingId` (int?), `CreatedAt`.
- `AssemblyMotionType` (`AssemblyMotionTypes`): `Id`, `PortalId`, `Code`, `Name`, `SortOrder` (int), `MinSubmitters` (int), `Deadline` (DateTime?), `CompetingDeadline` (DateTime?), `RequiredMajority`, `WikiTemplate` (string, ""), `OpenSlidesCategoryId` (int?).
- `AssemblyMotion` (`AssemblyMotions`): `Id`, `PortalId`, `TypeId`, `Number` (int?), `Title`, `Section` (string?), `TextMarkdown`, `TextHtml`, `ReasonMarkdown`, `ReasonHtml`, `Summary` (string?), `Keywords` (string?), `PriorDiscussion` (string?), `IsCompetingMotion`, `Status`, `Result`, `Revision` (int, 1), `CheckNote` (string?), `CheckedAt` (DateTime?), `CheckedByUserId` (Guid?), `CreatedByMemberId`, `CreatedAt`, `SubmittedAt` (DateTime?), `WithdrawnAt` (DateTime?), `CollectingReminderSentAt` (DateTime?), `WikiSyncState`, `WikiSyncedRevision` (int?), `WikiSyncedHash` (string?), `WikiLastSyncedAt` (DateTime?), `WikiLastError` (string?), `WikiFailureCount` (int), `WikiNextAttemptAt` (DateTime?), `WikiSyncRequestedAt` (DateTime?), `OpenSlidesMotionId` (int?), `OpenSlidesPushedRevision` (int?), `OpenSlidesPushedAt` (DateTime?), `OpenSlidesLastError` (string?), `DeletedAt` (DateTime?).
  - `Number != null` means "was submitted at some point": `[NotColumn] public bool WasSubmitted => Number.HasValue;`. A numbered motion back in `CollectingCoSubmitters` after an author edit is "in revision": `[NotColumn] public bool IsInRevision => Status == AssemblyMotionStatus.CollectingCoSubmitters && Number.HasValue;`. Everyone sees such a motion with its current text and the label "In Überarbeitung".
- `AssemblyMotionSubmitter` (`AssemblyMotionSubmitters`): `Id`, `MotionId`, `MemberId`, `IsAuthor`, `JoinedAt`, `JoinedRevision` (int).
- `AssemblyMotionRevision` (`AssemblyMotionRevisions`): `Id`, `MotionId`, `Revision` (int), `Title`, `Section`, `TextMarkdown`, `ReasonMarkdown`, `Summary`, `Keywords`, `PriorDiscussion`, `EditedByUserId` (Guid), `EditedAt`, `EditedByCommission`, `ChangeNote` (string?).
- `AssemblyMotionInquiry` (`AssemblyMotionInquiries`): `Id`, `MotionId`, `QuestionMarkdown`, `QuestionHtml`, `AskedByUserId`, `AskedAt`, `ResponseDueAt`, `AnswerMarkdown` (string?), `AnswerHtml` (string?), `AnsweredByMemberId` (Guid?), `AnsweredAt` (DateTime?), `ClosedAt` (DateTime?), `ClosedByUserId` (Guid?).
- `AssemblyMotionCompetition` (`AssemblyMotionCompetitions`): `Id`, `MotionAId`, `MotionBId`, `DeclaringMotionId`, `DeclaredByUserId`, `DeclaredAt`, `DeclaredByCommission`. `MotionAId` is the smaller Guid (`CompareTo`); `DeclaringMotionId` is the motion whose submitter or commission view declared the link (the spec's removal rights depend on it).
- `AssemblyMotionUpvote` (`AssemblyMotionUpvotes`): `Id`, `MotionId`, `MemberId`, `CreatedAt`.

No `ToDto` methods yet; DTOs come with the tasks that need them.

- [ ] **Step 3: Migration** `M004.cs`, `[Migration(006)] public class M004 : MigrationBase`. Create all eight tables with the columns above (nullable exactly where the type is nullable; strings that are non-nullable get `.NotNullable().WithDefaultValue("")` only for `IntroMarkdown`, `IntroHtml`, `WikiTemplate`; `Revision` default 1, `WikiFailureCount` default 0, enum columns default 0). String lengths: `Code` 16, `Name` 200, `Title` 500, `Section` 500, `WikiPagePrefix` 300, `WikiAssemblyLabel` 100, `Keywords` 500, `Summary` 2000, `PriorDiscussion` 2000, `CheckNote` 4000, `WikiSyncedHash` 64, `WikiLastError` 4000, `OpenSlidesLastError` 4000, `ChangeNote` 2000; Markdown/HTML/template columns `MEDIUMTEXT`.
  Foreign keys: portal `EventId` -> `Events.Id`; type `PortalId` -> portals; motion `PortalId`, `TypeId`, `CreatedByMemberId` -> `Members.Id`; submitter `MotionId`, `MemberId`; revision `MotionId`; inquiry `MotionId`; competition `MotionAId`, `MotionBId`; upvote `MotionId`, `MemberId`. Name them `FK_<Table>_<Column>_<TargetTable>_Id` like M003.
  Unique indexes: `IX_AssemblyMotionPortals_EventId`; `IX_AssemblyMotionTypes_Portal_Code` (PortalId, Code); `IX_AssemblyMotions_Portal_Type_Number` (PortalId, TypeId, Number) (MariaDB allows several NULLs); `IX_AssemblyMotionSubmitters_Motion_Member`; `IX_AssemblyMotionRevisions_Motion_Revision`; `IX_AssemblyMotionCompetitions_A_B`; `IX_AssemblyMotionUpvotes_Motion_Member`. Plain indexes: motions on `(PortalId, Status)` and on `WikiSyncState`.
  `Down()` drops the tables in reverse dependency order with `DropTableIfExists`.

- [ ] **Step 4: DbContext tables**: `public ITable<AssemblyMotionPortal> AssemblyMotionPortals => this.GetTable<AssemblyMotionPortal>();` and the same for the other seven (`AssemblyMotionTypes`, `AssemblyMotions`, `AssemblyMotionSubmitters`, `AssemblyMotionRevisions`, `AssemblyMotionInquiries`, `AssemblyMotionCompetitions`, `AssemblyMotionUpvotes`).

- [ ] **Step 5: Permission, role, options**
  - `PermissionIdentifier`: `public static readonly string ManageAssemblyMotions = "assemblymotions_manage";` Not in `DefaultOfficerPermissions`. Add it to `BoardWorkPermissions` so the board-work menu shows for commission members. In `SystemRole`: `public static readonly string MotionCommission = "motion_commission";`.
  - `PermissionRepository.SupplementDefaults`: `AddIfNotExists(PermissionIdentifier.ManageAssemblyMotions, "Antragskommission (Versammlungsanträge)", false);`
  - `RoleRepository.SupplementDefaults`: role `MotionCommission`, `Name = "Antragskommission"`, `Scope = RoleScope.ChapterScoped`, `IsSystem = true`, `InheritsToChildren = false`, `SetPermissions(role.Id, [PermissionIdentifier.ManageAssemblyMotions])`, created if missing exactly like the officer block.
  - `OptionRepository.SupplementDefaults`:
    - `wiki.api_url` "Wiki: API-URL", default `https://wiki.piratenpartei.de/api.php`
    - `wiki.bot_username` "Wiki: Bot-Benutzername"
    - `wiki.bot_password` "Wiki: Bot-Passwort" (`isSecret: true`)
    - `wiki.protection_level` "Wiki: Schutzstufe für Antragsseiten", default `Antrag`
    - `openslides.url` "OpenSlides: Basis-URL"
    - `openslides.username` "OpenSlides: Benutzername"
    - `openslides.password` "OpenSlides: Passwort" (`isSecret: true`)
    - All `OptionDataType.String`, not overridable, with a one-line German description.

- [ ] **Step 6: Test** `AssemblyMotionSeedingTests : RepositoryTestBase`:
  - `Permission_is_seeded_chapter_scoped`: after `Db.SupplementDefaults`-equivalent call used by other seeding tests (find one with `grep -rn SupplementDefaults Quartermaster.Server.Tests`), `Db.Permissions` has `assemblymotions_manage` with `Global == false`.
  - `Motion_commission_role_contains_permission`: role `motion_commission` exists, `IsSystem`, and its permissions are exactly `[assemblymotions_manage]`.
  - `Wiki_password_option_is_secret`: option definition `wiki.bot_password` has `IsSecret == true`, `openslides.password` too.
  - `Motion_number_is_unique_per_type`: insert two motions with the same `PortalId`, `TypeId`, `Number = 1` directly; the second insert throws. Two motions with `Number = null` insert fine.

- [ ] **Step 7: Build and run** `AssemblyMotionSeedingTests`. Expected: PASS (the test database fixture runs migrations, so a broken `M004` fails here).

### Task 2: Repositories and default types

**Files:**
- Create: `Quartermaster.Data/AssemblyMotions/AssemblyMotionDefaultTypes.cs`, `AssemblyMotionPortalRepository.cs`, `AssemblyMotionRepository.cs`
- Modify: `Quartermaster.Data/DbContext.cs` (`AddRepositories`)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionRepositoryTests.cs`

**Interfaces:**
- Consumes: Task 1 entities.
- Produces:

```csharp
public class AssemblyMotionPortalRepository {
    public AssemblyMotionPortalRepository(DbContext context, AuditLogRepository auditLog)
    public AssemblyMotionPortal? Get(Guid id)
    public AssemblyMotionPortal? GetByEvent(Guid eventId)
    public AssemblyMotionPortal Create(Guid eventId, DateTime opensAt, DateTime closesAt, DateTime nowUtc)   // seeds default types, audit "Created"
    public void Update(AssemblyMotionPortal portal)                                                     // audit LogFieldChange per changed field
    public List<AssemblyMotionType> GetTypes(Guid portalId)                                            // ordered by SortOrder, Code
    public AssemblyMotionType? GetType(Guid typeId)
    public AssemblyMotionType CreateType(AssemblyMotionType type)
    public void UpdateType(AssemblyMotionType type)
    public bool TypeHasMotions(Guid typeId)                                                             // any motion incl. drafts, excluding DeletedAt != null
    public void DeleteType(Guid typeId)
    public List<AssemblyMotionPortal> GetForChapters(IReadOnlyCollection<Guid>? chapterIds)             // null = all; joins Events, excludes deleted events
}

public class AssemblyMotionRepository {
    public AssemblyMotionRepository(DbContext context, AuditLogRepository auditLog)
    public DataConnectionTransaction BeginTransaction()
    public AssemblyMotion? Get(Guid id)                                     // ignores DeletedAt != null
    public AssemblyMotion? GetForUpdate(Guid id)                            // SELECT ... FOR UPDATE, call inside a transaction
    public void LockType(Guid typeId)                                       // SELECT Id FROM AssemblyMotionTypes WHERE Id = @id FOR UPDATE
    public int NextNumber(Guid portalId, Guid typeId)                       // max(Number)+1 over all motions of the type incl. withdrawn and deleted, 1 if none
    public void Insert(AssemblyMotion motion)
    public void Update(AssemblyMotion motion)                               // full row update, no audit (services audit)
    public List<AssemblyMotionSubmitter> GetSubmitters(Guid motionId)       // author first, then JoinedAt
    public Dictionary<Guid, List<AssemblyMotionSubmitter>> GetSubmitters(IReadOnlyCollection<Guid> motionIds)
    public void AddSubmitter(AssemblyMotionSubmitter submitter)
    public void RemoveSubmitter(Guid motionId, Guid memberId)
    public List<Guid> RemoveCoSubmitters(Guid motionId)                     // removes all non-authors, returns their member ids
    public void AddRevision(AssemblyMotionRevision revision)
    public List<AssemblyMotionRevision> GetRevisions(Guid motionId)         // ascending
    public AssemblyMotionRevision? GetRevision(Guid motionId, int revision)
    public List<AssemblyMotion> ListByPortal(Guid portalId)                 // non-deleted, all statuses
    public List<AssemblyMotion> ListOverdueCollecting(DateTime nowUtc)      // status CollectingCoSubmitters, used by the deadline job (filtering by effective deadline happens in the service)
    public List<AssemblyMotion> ListForMember(Guid portalId, Guid memberId) // authored (incl. drafts) or co-submitted
    public void SoftDelete(Guid id, DateTime nowUtc)
    public void MarkWikiPending(Guid motionId, DateTime nowUtc)            // sets WikiSyncState = Pending, WikiSyncRequestedAt = nowUtc, WikiNextAttemptAt = null, WikiFailureCount = 0
}
```

`GetForUpdate` and `LockType` use LinqToDB raw SQL inside the caller's transaction, for example:

```csharp
public AssemblyMotion? GetForUpdate(Guid id) {
    return _context.Query<AssemblyMotion>(
        $"SELECT * FROM {AssemblyMotion.TableName} WHERE Id = @id AND DeletedAt IS NULL FOR UPDATE",
        new DataParameter("id", id)).FirstOrDefault();
}
```

`AssemblyMotionDefaultTypes.Create(Guid portalId)` returns the six types from the spec (GP 5 TwoThirds, WP 5 TwoThirds, SÄA 5 TwoThirds, SBA 5 Simple, PP 1 Simple, SO 1 Simple) with `SortOrder` 10, 20, 30, 40, 50, 60, empty `WikiTemplate`, null deadlines. Names: Grundsatzprogramm, Wahlprogramm, Satzungsänderungsantrag, Satzungsbeiordnung, Positionspapier, Sonstiger Antrag.

Register both repositories with `services.AddScoped<...>()` in `AddRepositories`.

- [ ] **Step 1: Tests** `AssemblyMotionRepositoryTests : RepositoryTestBase` (seed `Event`/`Member` rows with `TestDataBuilder` over `Db`, see how other repository tests construct the builder):
  - `Create_seeds_six_default_types`: codes in order GP, WP, SÄA, SBA, PP, SO; MinSubmitters 5,5,5,5,1,1.
  - `NextNumber_counts_withdrawn_and_deleted`: motions of one type with numbers 1 (withdrawn) and 2 (soft-deleted) -> `NextNumber` returns 3; another type returns 1.
  - `RemoveCoSubmitters_keeps_author`: author + 2 co-submitters -> returns the 2 member ids, author remains.
  - `GetSubmitters_orders_author_first`.
  - `TypeHasMotions_ignores_soft_deleted_drafts`.
- [ ] **Step 2: Implement**, build, run `AssemblyMotionRepositoryTests`. Expected: PASS.

### Task 3: Berlin time, display number, effective deadline

**Files:**
- Create: `Quartermaster.Api/AssemblyMotions/BerlinTime.cs`, `AssemblyMotionNumber.cs`
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionDeadlines.cs`
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionDeadlinesTests.cs`

**Interfaces:**

```csharp
public static class BerlinTime {
    public static DateTime ToUtc(DateTime berlinLocal)          // Kind Unspecified in, Kind Utc out
    public static DateTime FromUtc(DateTime utc)                // Kind Unspecified out
    public static string FormatDeadline(DateTime utc)           // "12.03. um 23:59 Uhr"
}

public static class AssemblyMotionNumber {
    public static string Format(string typeCode, int number) => $"{typeCode}{number:000}";
}

public static class AssemblyMotionDeadlines {
    public static DateTime Effective(AssemblyMotionPortal portal, AssemblyMotionType type, bool isCompetingMotion)
    public static bool IsPast(DateTime effectiveDeadlineUtc, DateTime nowUtc) => nowUtc > effectiveDeadlineUtc;
}
```

`BerlinTime` resolves `TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")` once into a static readonly field (the id works on Linux and in Blazor WASM).

`Effective`: `isCompetingMotion && type.CompetingDeadline != null` -> `CompetingDeadline`; else `type.Deadline ?? portal.ClosesAt`; the result is never later than `portal.ClosesAt` (`ClosesAt` stops everything per spec).

- [ ] **Step 1: Tests**:
  - `BerlinTime_round_trips_across_dst`: `ToUtc(2027-03-12 23:59)` == `2027-03-12 22:59Z`; `ToUtc(2027-07-12 23:59)` == `2027-07-12 21:59Z`; `FromUtc` reverses both.
  - `FormatDeadline_uses_berlin_time`: `FormatDeadline(2027-07-12 21:59Z)` == `"12.07. um 23:59 Uhr"`.
  - `Format_pads_to_three_digits`: `Format("SÄA", 7)` == `"SÄA007"`, `Format("SO", 123)` == `"SO123"`.
  - `Effective_prefers_competing_deadline_only_when_flagged`, `Effective_falls_back_to_portal_close`, `Effective_never_after_portal_close` (type deadline after `ClosesAt` -> `ClosesAt`).
  - `IsPast_is_exclusive_at_the_deadline`: now == deadline -> false; now == deadline + 1 s -> true.
- [ ] **Step 2: Implement**, build, run `AssemblyMotionDeadlinesTests`. Expected: PASS.

### Task 4: Restricted Markdown for motion texts

**Files:**
- Create: `Quartermaster.Rendering/MarkdownFlavor.cs`, `Quartermaster.Rendering/MotionMarkdown.cs`
- Modify: `Quartermaster.Rendering/MarkdownService.cs` (flavour parameter), `Quartermaster.Blazor/Components/Inputs/MarkdownEditorWithPreview.razor.cs` (`Flavor` parameter passed to `MarkdownService.ToHtml`)
- Modify: i18n (`error.assembly_motion.markdown_html_not_allowed`, `error.assembly_motion.markdown_image_not_allowed`)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/MotionMarkdownTests.cs`

**Interfaces:**

```csharp
public enum MarkdownFlavor {
    Full = 0,
    Motion = 1
}

public enum MotionMarkdownProblem {
    RawHtml = 0,
    Image = 1
}

public static class MotionMarkdown {
    public static MarkdownPipeline Pipeline { get; }                       // CommonMark + UsePipeTables() + UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
    public static List<MotionMarkdownProblem> FindProblems(string markdown) // distinct problems, in enum order
}

// MarkdownService
public static string ToHtml(string markdown, SanitizationProfile profile = SanitizationProfile.Standard, MarkdownFlavor flavor = MarkdownFlavor.Full)
```

`FindProblems` parses with `MotionMarkdown.Pipeline` and walks `document.Descendants()`: `HtmlBlock` or `HtmlInline` -> `RawHtml`; `LinkInline` with `IsImage` -> `Image`. Everything else the motion pipeline knows is allowed: paragraphs, headings, emphasis, strikethrough, lists, links, autolinks, block quotes, inline code, code blocks, thematic breaks, pipe tables. Features of the full flavour (footnotes, task lists, definition lists, ...) render as plain text under the motion pipeline.

`MarkdownService.ToHtml` selects `MotionMarkdown.Pipeline` for `MarkdownFlavor.Motion` and the existing pipeline otherwise; existing callers are unchanged. `MarkdownEditorWithPreview` gets `[Parameter] public MarkdownFlavor Flavor { get; set; } = MarkdownFlavor.Full;`.

Validation, used by Task 10's `AssemblyMotionSaveRequestValidator` and Task 13's commission edit validator for `TextMarkdown` and `ReasonMarkdown`: `Must(markdown => !MotionMarkdown.FindProblems(markdown).Contains(MotionMarkdownProblem.RawHtml))` with `error.assembly_motion.markdown_html_not_allowed` "HTML ist im Antragstext nicht erlaubt. Bitte nur Markdown verwenden.", and the same for `Image` with `error.assembly_motion.markdown_image_not_allowed` "Bilder sind in Anträgen nicht erlaubt.".

- [ ] **Step 1: Tests**: `Html_block_is_a_problem`, `Inline_html_is_a_problem` (`Text <b>x</b>`), `Image_is_a_problem`, `Table_and_strikethrough_are_fine`, `Plain_motion_text_has_no_problems`, `Motion_flavour_renders_footnote_syntax_as_text` (`[^1]` produces no footnote section), `Full_flavour_unchanged` (output for a table plus footnote with the default arguments is identical to calling the full pipeline directly).
- [ ] **Step 2: Implement**, build, run `MotionMarkdownTests`. Expected: PASS.

### Task 5: Eligibility, access and visibility

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionEligibility.cs`, `EligibilityResult.cs`, `AssemblyMotionAccess.cs`, `AssemblyMotionViewer.cs`, `AssemblyMotionVisibility.cs`
- Modify: `Quartermaster.Server/Program.cs` (register scoped services next to the Keycloak block)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionEligibilityTests.cs`, `AssemblyMotionVisibilityTests.cs`

**Interfaces:**

```csharp
public record EligibilityResult(EligibilityProblem Problem, Member? Member) {
    public bool IsEligible => Problem == EligibilityProblem.None;
}

public class AssemblyMotionEligibility {
    public AssemblyMotionEligibility(DbContext db, ChapterRepository chapters)
    public EligibilityResult Check(Guid? userId, Event ev, DateTime nowUtc)
}

public class AssemblyMotionAccess {
    public AssemblyMotionAccess(PermissionContext perms)
    public bool IsCommission(Event ev) => _perms.Has(ev.ChapterId, PermissionIdentifier.ManageAssemblyMotions);
    public bool CanEditPortal(Event ev) => _perms.Has(ev.ChapterId, PermissionIdentifier.EditEvents);
}

public record AssemblyMotionViewer(Guid? MemberId, bool IsEligible, bool IsCommission);

public static class AssemblyMotionVisibility {
    public static bool CanSee(AssemblyMotion motion, AssemblyMotionViewer viewer)
    public static bool IsPublic(AssemblyMotion motion)
    public static bool CanSeeNames(AssemblyMotionViewer viewer, bool viewerIsSubmitter) => viewer.IsEligible || viewer.IsCommission || viewerIsSubmitter;
}
```

Eligibility, checked in this order (first failing rule wins):

```csharp
public EligibilityResult Check(Guid? userId, Event ev, DateTime nowUtc) {
    if (userId == null)
        return new EligibilityResult(EligibilityProblem.NotSignedIn, null);

    var member = _db.Members.FirstOrDefault(m => m.UserId == userId);
    if (member == null)
        return new EligibilityResult(EligibilityProblem.NoLinkedMember, null);
    if (member.AnonymizedAt != null)
        return new EligibilityResult(EligibilityProblem.Anonymized, member);
    if (member.IsFloating)
        return new EligibilityResult(EligibilityProblem.NoMemberNumber, member);
    if (member.IsPending)
        return new EligibilityResult(EligibilityProblem.Pending, member);
    if (member.IsExited(nowUtc))
        return new EligibilityResult(EligibilityProblem.Exited, member);
    if (!IsInEventChapter(member, ev))
        return new EligibilityResult(EligibilityProblem.OutsideChapter, member);
    return new EligibilityResult(EligibilityProblem.None, member);
}

private bool IsInEventChapter(Member member, Event ev) {
    if (member.ChapterId == null)
        return IsRootChapter(ev.ChapterId);
    return _chapters.GetDescendantIds(ev.ChapterId).Contains(member.ChapterId.Value);
}
```

`IsRootChapter(id)`: the chapter exists and its `ParentChapterId == null`. `GetDescendantIds` includes the chapter itself.

Visibility: every motion that has been released is public, whatever its status; only drafts are private to their author. `viewerIsSubmitter` (the viewer's member has a submitter row on the motion, current or left over from a lapse) only matters for names.

```csharp
public static bool IsPublic(AssemblyMotion motion)
    => motion.Status != AssemblyMotionStatus.Draft;

public static bool CanSee(AssemblyMotion motion, AssemblyMotionViewer viewer) {
    if (motion.Status == AssemblyMotionStatus.Draft)
        return viewer.MemberId != null && viewer.MemberId == motion.CreatedByMemberId;
    return true;
}
```

A numbered motion in revision is shown with its current text and the label "In Überarbeitung" (spec, Editing). The wiki mirror is unaffected: it still only covers motions where `WasSubmitted`.

- [ ] **Step 1: Tests** `AssemblyMotionEligibilityTests : RepositoryTestBase` with a hierarchy `Bund > Land > Kreis` (`SeedChapterHierarchy`), event on `Land`, a user linked to a member:
  - `Member_of_event_chapter_is_eligible`, `Member_of_descendant_chapter_is_eligible`, `Member_of_foreign_chapter_is_outside`, `Member_of_parent_chapter_is_outside` (member on `Bund`, event on `Land`).
  - `Exited_member_is_not_eligible` (ExitDate yesterday), `Exit_today_counts_as_exited`, `Pending_member_is_not_eligible`, `Floating_member_is_not_eligible` (MemberNumber null: update the seeded row), `Anonymized_member_is_not_eligible`, `User_without_member_is_not_eligible`, `Anonymous_is_not_signed_in`.
  - `Chapterless_member_is_eligible_for_federal_event` (event on `Bund`), `Chapterless_member_is_outside_for_state_event`.
  - `Negative_member_number_is_eligible` (member number -5).
- [ ] **Step 2: Tests** `AssemblyMotionVisibilityTests` (pure, construct entities in memory): `Released_motion_visible_to_everyone` (every status except `Draft`, with and without number), `Draft_visible_to_author_only` (also hidden from the commission), `Names_hidden_from_anonymous_only`.
- [ ] **Step 3: Implement**, register `AssemblyMotionEligibility` and `AssemblyMotionAccess` as scoped, build, run both classes. Expected: PASS.

### Task 6: Member lifecycle service

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionOutcome.cs`, `AssemblyMotionService.cs`, `AssemblyMotionDraft.cs`
- Modify: `Quartermaster.Api/I18n/I18nKey.cs`, `de.json`, `en.json` (error keys under `I18nKey.Error.AssemblyMotion`)
- Modify: `Program.cs` (register `AssemblyMotionService` scoped)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionServiceTests.cs`

**Interfaces:**

```csharp
public record AssemblyMotionOutcome(int StatusCode, string? ErrorKey, AssemblyMotion? Motion) {
    public bool Succeeded => ErrorKey == null;
    public static AssemblyMotionOutcome Ok(AssemblyMotion motion) => new(200, null, motion);
    public static AssemblyMotionOutcome Fail(int statusCode, string errorKey) => new(statusCode, errorKey, null);
}

public record AssemblyMotionDraft(Guid TypeId, string Title, string? Section, string TextMarkdown, string ReasonMarkdown,
    string? Summary, string? Keywords, string? PriorDiscussion);

public class AssemblyMotionService {
    public AssemblyMotionService(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        AuditLogRepository auditLog, DbContext db)
    public AssemblyMotionOutcome CreateDraft(AssemblyMotionPortal portal, Member author, Guid userId, AssemblyMotionDraft draft, DateTime nowUtc)
    public AssemblyMotionOutcome UpdateDraft(AssemblyMotion motion, Member author, Guid userId, AssemblyMotionDraft draft, int baseRevision, DateTime nowUtc)
    public AssemblyMotionOutcome Release(Guid motionId, Member author, DateTime nowUtc)
    public AssemblyMotionOutcome Join(Guid motionId, Member member, DateTime nowUtc)
    public AssemblyMotionOutcome Leave(Guid motionId, Member member, DateTime nowUtc)
    public AssemblyMotionOutcome Withdraw(Guid motionId, Member author, DateTime nowUtc)
    public AssemblyMotionOutcome DeleteDraft(Guid motionId, Member author, DateTime nowUtc)
    public int LapseOverdue(DateTime nowUtc)
}
```

Error keys (all under `error.assembly_motion.*`, German text in `de.json`):
- `not_found` "Antrag nicht gefunden." (404)
- `not_author` "Nur der Antragsteller kann diese Aktion ausführen." (403)
- `portal_not_open` "Das Antragsportal ist noch nicht geöffnet." (409)
- `portal_closed` "Das Antragsportal ist geschlossen." (409)
- `deadline_passed` "Die Frist für diese Antragsart ist am {deadline} abgelaufen." (409, built with `I18nParams.With(key, ("deadline", BerlinTime.FormatDeadline(effective)))`)
- `invalid_state` "Diese Aktion ist im aktuellen Status des Antrags nicht möglich." (409)
- `revision_conflict` "Der Antrag wurde inzwischen geändert. Bitte die Seite neu laden." (409)
- `already_submitter` "Du bist bereits Antragsteller dieses Antrags." (409)
- `not_submitter` "Du bist kein Mitantragsteller dieses Antrags." (409)
- `author_cannot_leave` "Der Antragsteller kann nicht austreten, sondern nur zurückziehen." (409)
- `type_not_in_portal` "Die Antragsart gehört nicht zu diesem Antragsportal." (422)

Rules (phase 1 scope; author edits beyond drafts come in Task 12):
- `CreateDraft`: refused before `OpensAt` and after `ClosesAt`; type must belong to the portal; inserts the motion (`Status = Draft`, `Revision = 1`, HTML via `MarkdownService.ToHtml(markdown, SanitizationProfile.Standard, MarkdownFlavor.Motion)`), the author submitter row (`IsAuthor = true`, `JoinedRevision = 1`) and revision 1; audit `Created`.
- `UpdateDraft`: only the author, only `Draft`, `baseRevision == motion.Revision` else `revision_conflict`; increments `Revision`, writes a revision row, updates the author's `JoinedRevision`; type may change while `Draft`. Audit `LogFieldChange` per changed text field.
- `Release`: only author, only `Draft`, `nowUtc >= OpensAt`, not past effective deadline. `MinSubmitters <= 1` -> `Submit`, else `CollectingCoSubmitters`. Audit `Released`.
- `Join`: in one transaction: `GetForUpdate`, status must be `CollectingCoSubmitters`, not past effective deadline, not already a submitter; insert submitter with `JoinedRevision = motion.Revision`; if submitter count >= `MinSubmitters` -> `Submit`. Audit `CoSubmitterJoined`.
- `Leave`: `CollectingCoSubmitters` only, author refused, removes the row. Audit `CoSubmitterLeft`.
- `Withdraw`: author only, any status except `Withdrawn`, `Lapsed`, not after `ClosesAt`. Sets `Withdrawn`, `WithdrawnAt`; if `WasSubmitted` -> `MarkWikiPending`. Audit `Withdrawn`.
- `DeleteDraft`: author only, `Draft` only, soft delete.
- `Submit` (private, inside the caller's transaction):

```csharp
private void Submit(AssemblyMotion motion, DateTime nowUtc) {
    if (motion.Number == null) {
        _motions.LockType(motion.TypeId);
        motion.Number = _motions.NextNumber(motion.PortalId, motion.TypeId);
    }
    motion.Status = AssemblyMotionStatus.Submitted;
    motion.SubmittedAt = nowUtc;
    _motions.Update(motion);
    _motions.MarkWikiPending(motion.Id, nowUtc);
    _auditLog.Log("AssemblyMotion", motion.Id, "Submitted", "Number", null, motion.Number.ToString());
}
```

  `Release` also runs in a transaction with `GetForUpdate`. `MarkWikiPending` is called unconditionally; the wiki sync (Task 24) ignores portals without the mirror.
- `LapseOverdue`: loads `ListOverdueCollecting`, computes the effective deadline per motion, sets `Lapsed` where past; if `WasSubmitted` -> `MarkWikiPending`; audit `Lapsed`; returns the count.

- [ ] **Step 1: Tests** `AssemblyMotionServiceTests : RepositoryTestBase` (construct the service directly over `Db`; helper methods `SeedPortal(opensAt, closesAt)`, `SeedEligibleMember()`):
  - `Release_with_one_required_submits_immediately_with_number_1`.
  - `Release_with_five_required_collects`.
  - `Fifth_join_submits_and_numbers`: author + 4 joins -> `Submitted`, `Number == 1`, `WikiSyncState == Pending`.
  - `Numbers_are_sequential_per_type`: two SO motions -> 1 and 2; a PP motion -> 1.
  - `Withdrawn_number_is_not_reused`.
  - `Join_refused_after_deadline` (deadline error key contains `deadline=`).
  - `Release_refused_one_minute_after_deadline`, `Release_allowed_one_minute_before_deadline`.
  - `Create_refused_before_opens_and_after_closes`.
  - `Leave_refused_for_author`, `Join_twice_refused`.
  - `UpdateDraft_with_stale_revision_conflicts`.
  - `LapseOverdue_lapses_only_past_effective_deadline`: one past, one with competing deadline still open (flag set, `CompetingDeadline` in the future).
  - `Concurrent_last_joins_submit_once`: motion with `MinSubmitters = 3`, author + 1 joined; two further members join in parallel on two separate `DbContext` instances (`Worker.CreateDbContext()`), each with its own service; `Task.WhenAll`. Assert: exactly 3 or 4 submitter rows depending on whether the late join was refused (the second join after submission must return `invalid_state`), status `Submitted`, `Number == 1`, exactly one `Submitted` audit entry.
  - `Concurrent_submissions_get_distinct_numbers`: two different one-submitter motions of the same type released in parallel on separate contexts -> numbers {1, 2}.
- [ ] **Step 2: Implement**, build, run `AssemblyMotionServiceTests`. Expected: PASS.

### Task 7: Deadline background job

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionDeadlineHostedService.cs`
- Modify: `Program.cs` (`builder.Services.AddHostedService<AssemblyMotionDeadlineHostedService>();` next to the retention service)

Copy the loop shape of `RetentionAnonymizationHostedService`: `RunInterval = TimeSpan.FromMinutes(5)`, a scope per run, call `AssemblyMotionService.LapseOverdue(DateTime.UtcNow)`, log the count at Information when > 0. No test of the hosted service itself (the work method is covered in Task 6).

- [ ] **Step 1: Implement**, build. Expected: build succeeds.

### Task 8: Portal configuration endpoints

**Files:**
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionPortalDTOs.cs` (`AssemblyMotionPortalDTO`, `AssemblyMotionTypeDTO`, `AssemblyMotionPortalSettingsDTO`, `AssemblyMotionTypeSettingsDTO`)
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionPortalRequests.cs` (`AssemblyMotionPortalEnableRequest`, `AssemblyMotionPortalUpdateRequest`, `AssemblyMotionTypeCreateRequest`, `AssemblyMotionTypeUpdateRequest`, `AssemblyMotionTypeDeleteRequest`, `AssemblyMotionPortalByEventRequest`, `AssemblyMotionPortalSettingsRequest`)
- Create: `Quartermaster.Server/AssemblyMotions/Endpoints/Portal/` one endpoint class per route below, plus validators
- Modify: `Quartermaster.Data/AssemblyMotions/AssemblyMotionPortal.cs`, `AssemblyMotionType.cs` (`ToDto`, `ToSettingsDto`)
- Test: `Quartermaster.Server.Tests/Integration/AssemblyMotions/AssemblyMotionPortalEndpointTests.cs`, `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionPortalValidatorTests.cs`

**Routes:**
- `GET /api/assemblymotions/portals/by-event/{EventId}` anonymous -> `AssemblyMotionPortalDTO` (Id, EventId, EventName = `PublicName`, EventDate, OpensAt, ClosesAt, IntroHtml, PrioritizationEnabled, PrioritizationStartsAt, PrioritizationEndsAt, Types: `AssemblyMotionTypeDTO` with Id, Code, Name, SortOrder, MinSubmitters, Deadline, CompetingDeadline, RequiredMajority). 404 when no portal or the event is not visible to the caller (reuse the visibility rules of `EventDetailEndpoint`: `Private` needs `ViewEvents` on the chapter, `MembersOnly` needs sign-in).
- `GET /api/assemblymotions/portals/{Id}/settings` (`events_edit`) -> `AssemblyMotionPortalSettingsDTO` = portal DTO fields + IntroMarkdown, WikiMirrorEnabled, WikiPagePrefix, WikiAssemblyLabel, OpenSlidesMeetingId, Types as `AssemblyMotionTypeSettingsDTO` (+ WikiTemplate, HasMotions).
- `POST /api/assemblymotions/portals` (`events_edit` on the event's chapter) body `{ EventId, OpensAt, ClosesAt }` -> settings DTO; 409 when a portal exists.
- `PUT /api/assemblymotions/portals/{Id}` (`events_edit`) body: OpensAt, ClosesAt, IntroMarkdown, PrioritizationEnabled, PrioritizationStartsAt, PrioritizationEndsAt, WikiMirrorEnabled, WikiPagePrefix, WikiAssemblyLabel, OpenSlidesMeetingId.
- `POST /api/assemblymotions/portals/{PortalId}/types`, `PUT /api/assemblymotions/types/{Id}`, `DELETE /api/assemblymotions/types/{Id}` (`events_edit`).

Validation (validators with `I18nKey.Error.AssemblyMotion.Portal.*` / `.Type.*`):
- `OpensAt < ClosesAt`.
- Prioritization enabled -> both window dates set, start < end, end <= ClosesAt.
- Wiki mirror enabled -> `WikiPagePrefix` and `WikiAssemblyLabel` non-empty. (Credential validation on enable comes in Task 25; until then enabling is allowed.)
- Type: `Code` 1-16 chars of letters (incl. umlauts), unique per portal (endpoint check, 409); `Name` required; `MinSubmitters >= 1`; `CompetingDeadline`, when set, later than `Deadline ?? ClosesAt`.
- Type update: `Code` change refused (409 `type_has_motions`) when `TypeHasMotions`; delete refused likewise.
- `IntroHtml` = `MarkdownService.ToHtml(IntroMarkdown)`. Audit through the repository.
- Date inputs arrive as UTC in the DTOs (the Blazor page converts with `BerlinTime.ToUtc`).

- [ ] **Step 1: Validator tests** for each rule above (`TestValidate`, `ShouldHaveValidationErrorFor`).
- [ ] **Step 2: Endpoint tests** `AssemblyMotionPortalEndpointTests : IntegrationTestBase`:
  - `Enable_requires_events_edit` (403 without, 200 with `events_edit` on the event chapter, 403 with it only on another chapter).
  - `Enable_twice_conflicts`.
  - `Enable_seeds_default_types`.
  - `ByEvent_is_anonymous_for_public_event` and `ByEvent_hides_private_event_from_anonymous` (404).
  - `Type_code_change_refused_when_motions_exist`, `Type_delete_refused_when_motions_exist`.
  - `Update_rejects_prioritization_end_after_close` (400 with field error).
  Mutating calls use `AuthenticatedClientWithCsrfAsync`.
- [ ] **Step 3: Implement**, build, run both classes. Expected: PASS.

### Task 9: Public endpoints

**Files:**
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionPublicDTOs.cs` (`AssemblyMotionPublicListItemDTO`, `AssemblyMotionPublicDTO`, `AssemblyMotionPublicListResponse : IPaginatedResponse<AssemblyMotionPublicListItemDTO>`, `AssemblyMotionCompetitionPublicDTO`)
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionPublicListRequest.cs` (`: IPaginatedRequest`, `PortalId`, `TypeId?`, `Status?`, `Search?`), `AssemblyMotionPublicDetailRequest.cs`
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionPublicView.cs` (builds public DTOs from the motion's current fields)
- Create: `Quartermaster.Server/AssemblyMotions/Endpoints/Public/AssemblyMotionPublicListEndpoint.cs`, `AssemblyMotionPublicDetailEndpoint.cs`
- Test: `Quartermaster.Server.Tests/Integration/AssemblyMotions/AssemblyMotionPublicEndpointTests.cs`

**Routes:** `GET /api/assemblymotions/portals/{PortalId}/motions` and `GET /api/assemblymotions/motions/{Id}/public`, both `AllowAnonymous()`, both 404 when the event is not visible to the caller (same rule as Task 8).

**DTO fields (no names, no member ids, no member numbers, no user ids):**
- List item: Id, TypeId, TypeCode, TypeName, DisplayNumber, Title, Status, IsInRevision, Result, SubmittedAt.
- Detail: list item fields + Section, TextHtml, ReasonHtml, Summary, Keywords, PriorDiscussion, CheckNote, RequiredMajority, Competitions (filled in phase 3; empty list until then), UpvoteCount (int?, null until phase 3).

**Content source:** `AssemblyMotionPublicView.Build(motion, type)` uses the motion's current fields, so every viewer sees the same text. `IsInRevision` lets the UI show "In Überarbeitung" instead of the plain collecting status. Only motions where `AssemblyMotionVisibility.IsPublic` is true are listed or returned.

List: filter by `TypeId`, `Status`, `Search` (matches title case-insensitively or the display number, e.g. "SÄA003" or "SÄA3"); order by type `SortOrder`, then `Number`; paginated (`PaginationValidationExtensions`).

- [ ] **Step 1: Tests** `AssemblyMotionPublicEndpointTests`:
  - `Lists_every_motion_except_drafts`: seed a draft, a collecting motion without number, a submitted, an admitted, a withdrawn motion without number and a withdrawn motion with number; the list contains everything but the draft, numbered motions first within a type.
  - `Anonymous_responses_contain_no_submitter_names`: seed submitters with first name `Zebulon` and last name `Quaxwell`; the raw JSON body of list and detail does not contain `Zebulon`, `Quaxwell` or the member number.
  - `Edited_motion_shows_current_text_in_revision_anonymously`: numbered motion with text "Alt", then (direct DB update in the test, the service rule comes in Task 12) text "Neu", `Revision = 2`, status `CollectingCoSubmitters` -> anonymous list and detail show "Neu", `IsInRevision == true`, no names.
  - `Search_matches_display_number_without_padding`.
  - `Private_event_portal_returns_404_anonymously`.
- [ ] **Step 2: Implement**, build, run. Expected: PASS.

### Task 10: Member endpoints

**Files:**
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionMemberDTOs.cs` (`AssemblyMotionPortalContextDTO`, `AssemblyMotionDetailDTO`, `AssemblyMotionSubmitterDTO`, `AssemblyMotionMineItemDTO`)
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionMemberRequests.cs` (`AssemblyMotionSaveRequest` with TypeId, Title, Section, TextMarkdown, ReasonMarkdown, Summary, Keywords, PriorDiscussion, BaseRevision; `AssemblyMotionIdRequest`; `AssemblyMotionPortalIdRequest`)
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionDetailBuilder.cs` (member/commission detail DTO; resolves names)
- Create: endpoints in `Quartermaster.Server/AssemblyMotions/Endpoints/Member/`: `PortalContextEndpoint`, `CollectingListEndpoint`, `DetailEndpoint`, `CreateEndpoint`, `UpdateEndpoint`, `ReleaseEndpoint`, `JoinEndpoint`, `LeaveEndpoint`, `WithdrawEndpoint`, `DeleteDraftEndpoint`, `MineEndpoint` (class names prefixed `AssemblyMotion`), `AssemblyMotionSaveRequestValidator`
- Test: `Quartermaster.Server.Tests/Integration/AssemblyMotions/AssemblyMotionMemberEndpointTests.cs`, `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionSaveRequestValidatorTests.cs`

**Routes:**
- `GET /api/assemblymotions/portals/{PortalId}/context` (anonymous allowed) -> `AssemblyMotionPortalContextDTO { bool IsSignedIn, bool IsEligible, EligibilityProblem Problem, bool IsCommission, bool CanEditPortal }`.
- `GET /api/assemblymotions/portals/{PortalId}/collecting` (eligible or commission) -> paginated `AssemblyMotionPublicListItemDTO` of motions in `CollectingCoSubmitters` without number, plus `SubmitterCount` and `MinSubmitters` and `EffectiveDeadline` (use a dedicated `AssemblyMotionCollectingItemDTO`).
- `GET /api/assemblymotions/motions/{Id}` (signed in) -> `AssemblyMotionDetailDTO`; 404 when `CanSee` is false.
- `POST /api/assemblymotions/portals/{PortalId}/motions` (eligible) -> detail DTO.
- `PUT /api/assemblymotions/motions/{Id}` (author) -> `UpdateDraft` for now; Task 12 extends it.
- `POST .../motions/{Id}/release`, `/join`, `/leave`, `/withdraw`, `DELETE .../motions/{Id}`.
- `GET /api/assemblymotions/portals/{PortalId}/mine` -> list of `AssemblyMotionMineItemDTO` (Id, DisplayNumber, Title, Status, IsAuthor, OpenInquiryCount (0 until phase 2)).

Every mutating member endpoint first runs `AssemblyMotionEligibility.Check`; ineligible -> 403 with error key `error.assembly_motion.not_eligible` ("Du kannst an diesem Antragsportal nicht teilnehmen."). Withdraw and leave do not require eligibility (an exited member can still withdraw their own motion or leave).

`AssemblyMotionDetailDTO`: all public detail fields + TextMarkdown, ReasonMarkdown, Revision, IsCompetingMotion, MinSubmitters, EffectiveDeadline, Submitters (`AssemblyMotionSubmitterDTO` MemberId, DisplayName, IsAuthor, JoinedAt, JoinedRevision, MemberNumber (int?, filled only for the commission)), IsAuthor, IsSubmitter, CanEdit, CanRelease, CanJoin, CanLeave, CanWithdraw, CanDelete. Names: `"{FirstName} {LastName}"` from `Member`; anonymized or missing -> `"Ehemaliges Mitglied"`. Submitters are included only when `CanSeeNames`; otherwise the list is empty. The `Can*` flags are computed by the same rule methods the service uses (expose them as `public bool CanJoin(...)` etc. on `AssemblyMotionService` so UI flags and enforcement cannot drift).

Validator `AssemblyMotionSaveRequestValidator`: Title required, max 500; TextMarkdown required; ReasonMarkdown required; Summary max 2000; Keywords max 500; PriorDiscussion max 2000; Section max 500.

- [ ] **Step 1: Validator tests** for each rule.
- [ ] **Step 2: Endpoint tests** `AssemblyMotionMemberEndpointTests` (seed a user via `SeedAuthenticatedUser` and a member via `SeedMember(userId: user.Id, chapterId: ...)`):
  - `Create_requires_eligibility` (member of a foreign chapter -> 403 `not_eligible`).
  - `Full_flow_release_join_submit`: author creates and releases an SÄA (MinSubmitters lowered to 2 for the test); second member joins; detail shows `Submitted`, `DisplayNumber == "SÄA001"`, both names.
  - `Collecting_list_hidden_from_ineligible_member` (403).
  - `Draft_detail_404_for_other_member`.
  - `Member_numbers_only_for_commission`: detail for a co-submitter has all `MemberNumber == null`; detail for a user with `assemblymotions_manage` on the event chapter has them.
  - `Exited_author_can_withdraw`.
  - `Mine_lists_authored_and_cosubmitted`.
- [ ] **Step 3: Implement**, build, run both classes. Expected: PASS.

### Task 11: Blazor pages for phase 1, docs, changelog

**Files:**
- Create: `Quartermaster.Blazor/Components/AssemblyMotions/AssemblyMotionStatusBadge.razor(.cs)` (pattern: `MotionApprovalBadge`; parameters `Status` and `IsInRevision`; `IsInRevision` shows "In Überarbeitung" instead of "Sucht Mitantragsteller")
- Create: `Quartermaster.Blazor/Components/AssemblyMotions/AssemblyMotionPortalSettings.razor(.cs)` (section embedded in the admin event detail page)
- Create: `Quartermaster.Blazor/Pages/AssemblyMotions/AssemblyMotionPortal.razor(.cs)` route `/Events/{EventId:guid}/Motions`
- Create: `Quartermaster.Blazor/Pages/AssemblyMotions/AssemblyMotionDetail.razor(.cs)` route `/AssemblyMotions/{Id:guid}`
- Create: `Quartermaster.Blazor/Pages/AssemblyMotions/AssemblyMotionEditor.razor(.cs)` routes `/Events/{EventId:guid}/Motions/New` and `/AssemblyMotions/{Id:guid}/Edit`
- Create: `Quartermaster.Blazor/Pages/AssemblyMotions/AssemblyMotionCollecting.razor(.cs)` route `/Events/{EventId:guid}/Motions/Collecting`
- Create: `Quartermaster.Blazor/Pages/AssemblyMotions/AssemblyMotionMine.razor(.cs)` route `/Events/{EventId:guid}/Motions/Mine`
- Modify: `Quartermaster.Blazor/Pages/Administration/EventDetail.razor` (add `<AssemblyMotionPortalSettings EventId="..." ChapterId="..." />` card above the audit log card)
- Modify: `Quartermaster.Blazor/Pages/Events/PublicEventDetail.razor(.cs)` (when `/api/assemblymotions/portals/by-event/{id}` returns a portal: button "Antragsportal" linking to `/Events/{id}/Motions`)
- Modify: `I18nKey.cs` (`I18nKey.Ui.AssemblyMotion.*`), `de.json`, `en.json`
- Modify: `Quartermaster.Documentation/Translations.md` (new section "Antragsportal" with the spec's terms table)
- Create: `Quartermaster.Server/Changelog/V4.md`

Pages:
- Portal page: intro HTML, a deadline table per type (name, code, required submitters, deadline in Berlin time via `BerlinTime.FromUtc`, competing deadline), motions grouped by type with `AssemblyMotionStatusBadge`, filter by type and status, search box, pagination. When the context says eligible: buttons "Neuer Antrag", "Anträge, die Mitantragsteller suchen", "Meine Anträge". When signed in but ineligible: an info alert with the reason (one i18n text per `EligibilityProblem`). When `IsCommission`: button "Antragskommission" (target page comes in Task 16; hide until then).
- Detail page: loads the member detail when signed in (falls back to the public detail on 401), shows number, type, title, section, text, reason, summary, keywords, prior discussion, status, check note; submitters with "(Antragsteller)" marker; action buttons from the `Can*` flags with `ConfirmDialog` for withdraw, leave and join (join confirm text: title + "Mit dem Beitritt wirst du Mitantragsteller. Dein Name ist für angemeldete Mitglieder sichtbar."); share link hint while collecting.
- Editor: type select showing deadline and required submitters of the chosen type; fields; `MarkdownEditorWithPreview` with `Flavor="MarkdownFlavor.Motion"` for text and reason, with a short help line listing the allowed formatting (Überschriften, fett, kursiv, durchgestrichen, Listen, Links, Zitate, Code, Tabellen; kein HTML, keine Bilder); "Als Entwurf speichern" and "Freigeben" (release saves first). Sends `BaseRevision`; on 409 `revision_conflict` show the message and offer reload.
- Portal settings section: when no portal, button "Antragsportal aktivieren" with opens/closes inputs (`datetime-local`, Berlin time). When present: dates, intro (`MarkdownEditorWithPreview`), prioritization toggle and window, wiki toggle, prefix, label, OpenSlides meeting id, types table with inline edit/add/delete (delete and code disabled when `HasMotions`, with a tooltip). Inputs that cannot be edited get both `readonly` and `disabled`.
- All markup text through `I18n[...]`.

Changelog `V4.md` first section:

```
### Antragsportal für Parteitage

Für Parteitage und andere Versammlungen lässt sich am Event ein Antragsportal einrichten. Mitglieder schreiben dort ihre Anträge, stellen sie zur Unterstützung frei und treten anderen Anträgen als Mitantragsteller bei. Sobald genug Antragsteller beisammen sind, ist der Antrag eingereicht und erhält seine Nummer.
```

- [ ] **Step 1: Implement** pages and docs. Build.
- [ ] **Step 2: Render check**: start the local server (see the checkpoint task for the commands) and use the headless Playwright script pattern from earlier sessions (`/media/SMB/Quartermaster/Quartermaster.Server.Tests/bin/Debug/net10.0/.playwright/node/linux-x64/node` with `executablePath` of `chrome-headless-shell`, login via `/Login/Manual`) to screenshot the portal page, the editor and the settings section into the scratchpad; look at the screenshots. Do not use the user's Chrome.

### Checkpoint 1

- [ ] Build: `cd /media/SMB/Quartermaster && dotnet build Quartermaster.sln -m:2 -v:q -nologo`.
- [ ] Run each phase-1 test class with its treenode filter: `AssemblyMotionSeedingTests`, `AssemblyMotionRepositoryTests`, `AssemblyMotionDeadlinesTests`, `MotionMarkdownTests`, `AssemblyMotionEligibilityTests`, `AssemblyMotionVisibilityTests`, `AssemblyMotionServiceTests`, `AssemblyMotionPortalValidatorTests`, `AssemblyMotionPortalEndpointTests`, `AssemblyMotionPublicEndpointTests`, `AssemblyMotionSaveRequestValidatorTests`, `AssemblyMotionMemberEndpointTests`.
- [ ] Restart the local server so the user can test (kill the running binary by PID, `pgrep -x Quartermaster.S`, then start it again the same way it was started before).
- [ ] Stop. Report to the user: what is testable, the migration `M004` was applied to the dev database, and what phase 2 contains.

---

# Phase 2: Editing, Revisions, Commission, Inquiries, Notifications

### Task 12: Author editing rules, revision history, text diff

**Files:**
- Create: `Quartermaster.Api/AssemblyMotions/TextDiff.cs` (`TextDiff`, `TextDiffLine`, `TextDiffKind` in one file: the record and enum are data types)
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionRevisionDTO.cs`
- Create: `Quartermaster.Server/AssemblyMotions/Endpoints/Member/AssemblyMotionRevisionsEndpoint.cs`
- Modify: `AssemblyMotionService.cs` (`UpdateDraft` becomes `UpdateByAuthor`), `AssemblyMotionUpdateEndpoint.cs`, `AssemblyMotionRevision.cs` (`ToDto`)
- Modify: i18n (`error.assembly_motion.edit_locked`)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/TextDiffTests.cs`, extend `AssemblyMotionServiceTests.cs`, extend `AssemblyMotionMemberEndpointTests.cs`

**Interfaces:**

```csharp
public enum TextDiffKind { Unchanged = 0, Added = 1, Removed = 2 }
public record TextDiffLine(TextDiffKind Kind, string Text);
public static class TextDiff {
    public static List<TextDiffLine> Lines(string oldText, string newText)
}

public AssemblyMotionOutcome UpdateByAuthor(AssemblyMotion motion, Member author, Guid userId, AssemblyMotionDraft draft, int baseRevision, DateTime nowUtc)
public bool CanEditAsAuthor(AssemblyMotion motion, AssemblyMotionType type, AssemblyMotionPortal portal, Guid? memberId, DateTime nowUtc)
```

`TextDiff.Lines`: split on `\n` (normalize `\r\n`), longest common subsequence over lines, emit in order (removed before added for a changed region). When `oldLines * newLines > 4_000_000`, return all old lines as `Removed` followed by all new lines as `Added`.

`UpdateByAuthor` rules (spec "Editing" by the author), inside a transaction with `GetForUpdate`:
- Not the author -> `not_author`. `baseRevision != motion.Revision` -> `revision_conflict`.
- A save without any content change (Title, Section, TextMarkdown, ReasonMarkdown, Summary, Keywords, PriorDiscussion, TypeId all equal) returns `Ok` without a new revision.
- `TypeId` may change only while `motion.Number == null`; otherwise `invalid_state`.
- `Draft`: save, `Revision++`, revision row, author `JoinedRevision = Revision`.
- `CollectingCoSubmitters` before the effective deadline: as Draft, plus `RemoveCoSubmitters`, `CollectingReminderSentAt = null`; when the motion has a number (in revision) also `MarkWikiPending`.
- `Submitted` before the effective deadline: as Collecting; if `type.MinSubmitters > 1` -> status `CollectingCoSubmitters` (in revision: portal, public view and wiki show the new text as "In Überarbeitung"), `SubmittedAt = null`, keep `Number`; else stays `Submitted` with `SubmittedAt = nowUtc`. Both cases `MarkWikiPending`.
- `Admitted`, `FormallyInsufficient`, `Withdrawn`, `Lapsed`, or after the effective deadline, or after `ClosesAt` -> `edit_locked` "Der Antrag kann nicht mehr geändert werden. Bitte wende dich an die Antragskommission.".
- Audit: `LogFieldChange` per changed field (full old and new Markdown, like board motions) and `CoSubmittersReset` with the count when co-submitters were removed.

Revisions endpoint `GET /api/assemblymotions/motions/{Id}/revisions`: allowed for eligible members (for non-draft motions), submitters and the commission; never anonymous. Returns `AssemblyMotionRevisionDTO` list (Revision, Title, Section, TextMarkdown, ReasonMarkdown, Summary, Keywords, PriorDiscussion, EditedAt, EditedByCommission, ChangeNote). Drafts: author only.

- [ ] **Step 1: TextDiff tests**: identical texts all `Unchanged`; one inserted line; one removed line; changed middle line yields `Removed` then `Added`; CRLF input equals LF input; huge input fallback.
- [ ] **Step 2: Service tests**: `Edit_while_collecting_removes_cosubmitters`, `Edit_submitted_five_type_returns_to_collecting_keeps_number`, `Edit_submitted_one_type_stays_submitted_and_queues_wiki`, `Edit_after_deadline_locked`, `Edit_admitted_locked`, `Type_change_refused_after_number`, `Noop_save_creates_no_revision`, `Stale_base_revision_conflicts`.
- [ ] **Step 3: Endpoint tests**: `Revisions_hidden_from_anonymous` (401), `Revisions_visible_to_eligible_member`.
- [ ] **Step 4: Implement**, build, run `TextDiffTests`, `AssemblyMotionServiceTests`, `AssemblyMotionMemberEndpointTests`. Expected: PASS.

### Task 13: Commission service, dashboard, formal check, commission edit, managed portals

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionCommissionService.cs`
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionCommissionDTOs.cs` (`AssemblyMotionDashboardItemDTO`, `AssemblyMotionDashboardResponse : IPaginatedResponse<AssemblyMotionDashboardItemDTO>` with extra `int FailedWikiSyncCount`, `AssemblyMotionCommissionInfoDTO`, `AssemblyMotionManagedPortalDTO`)
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionCommissionRequests.cs` (`AssemblyMotionDashboardRequest : IPaginatedRequest` with PortalId, TypeId?, Status?, HasOpenInquiry?, WikiSyncState?, Search?; `AssemblyMotionCheckRequest` Id, Status, CheckNote; `AssemblyMotionCommissionEditRequest` = save fields + BaseRevision + ChangeNote)
- Create: `Quartermaster.Server/AssemblyMotions/Endpoints/Commission/AssemblyMotionDashboardEndpoint.cs`, `AssemblyMotionCheckEndpoint.cs`, `AssemblyMotionCommissionEditEndpoint.cs`, `AssemblyMotionCommissionWithdrawEndpoint.cs`, `AssemblyMotionManagedPortalsEndpoint.cs`, validators
- Modify: `AssemblyMotionDetailBuilder.cs` (fill `Commission` sub-DTO for the commission), `AssemblyMotionDetailDTO` (`AssemblyMotionCommissionInfoDTO? Commission`)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionCommissionServiceTests.cs`, `Quartermaster.Server.Tests/Integration/AssemblyMotions/AssemblyMotionCommissionEndpointTests.cs`

**Interfaces:**

```csharp
public class AssemblyMotionCommissionService {
    public AssemblyMotionCommissionService(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals, AuditLogRepository auditLog, DbContext db)
    public AssemblyMotionOutcome Check(Guid motionId, AssemblyMotionStatus decision, string? checkNote, Guid userId, DateTime nowUtc)
    public AssemblyMotionOutcome Edit(Guid motionId, Guid userId, AssemblyMotionDraft draft, int baseRevision, string changeNote, DateTime nowUtc)
    public AssemblyMotionOutcome Withdraw(Guid motionId, Guid userId, DateTime nowUtc)
}
```

Rules:
- `Check`: motion status must be `Submitted`, `Admitted` or `FormallyInsufficient`; `decision` must be one of these three; `FormallyInsufficient` needs a non-empty `checkNote` (`error.assembly_motion.check_note_required`). Sets `Status`, `CheckNote` (trimmed, null when empty), `CheckedAt`, `CheckedByUserId`; `MarkWikiPending`; audit `LogFieldChange("AssemblyMotion", id, "Status", old, new)` and `CheckNote`.
- `Edit`: allowed when `motion.Number != null` and status is not `CollectingCoSubmitters` (`invalid_state` otherwise); `changeNote` required (`error.assembly_motion.change_note_required`); base revision check; `TypeId` cannot change; no co-submitter reset; `Revision++`, revision row with `EditedByCommission = true` and `ChangeNote`; `MarkWikiPending`; audit per field.
- `Withdraw`: any status except `Withdrawn` and `Lapsed`; sets `Withdrawn`, `WithdrawnAt`; `MarkWikiPending` when `WasSubmitted`; audit `Withdrawn` with field `ByCommission`.

Endpoints (all commission-only via `AssemblyMotionAccess.IsCommission(event)`, else 403):
- `GET /api/assemblymotions/portals/{PortalId}/dashboard`: motions from `CollectingCoSubmitters` on (no drafts), filters, search like Task 9, order type `SortOrder`, `Number` (nulls last), `CreatedAt`. Item: Id, DisplayNumber (null without number), Title, TypeCode, Status, Result, SubmitterCount, MinSubmitters, OpenInquiryCount (0 until Task 14), EffectiveDeadline, DeadlinePassed, WikiSyncState, Revision, OpenSlidesPushedRevision. `FailedWikiSyncCount` = motions of the portal in `Failed` or `ProtectionFailed`.
- `POST /api/assemblymotions/motions/{Id}/check`, `PUT /api/assemblymotions/motions/{Id}/commission-edit`, `POST /api/assemblymotions/motions/{Id}/commission-withdraw`.
- `GET /api/assemblymotions/portals/managed`: portals whose event the caller is commission for (load all portals with their events, filter by `IsCommission`), as `AssemblyMotionManagedPortalDTO` (PortalId, EventId, EventName, EventDate, ChapterName).
- Member detail endpoint: for the commission, `Commission` = CheckedAt, CheckedByName (user display name), WikiSyncState, WikiSyncedRevision, WikiLastSyncedAt, WikiLastError, OpenSlidesMotionId, OpenSlidesPushedRevision, OpenSlidesPushedAt, OpenSlidesLastError. The commission sees collecting motions but not drafts (Task 5 rule).

- [ ] **Step 1: Service tests**: `Insufficient_requires_note`, `Decision_can_be_corrected` (Admitted -> FormallyInsufficient -> Admitted), `Check_refused_while_collecting`, `Commission_edit_keeps_cosubmitters_and_bumps_submitted_revision`, `Commission_edit_requires_change_note`, `Commission_withdraw_after_close_allowed`.
- [ ] **Step 2: Endpoint tests**: `Dashboard_requires_commission_on_event_chapter` (permission on a sub-chapter does not count), `Dashboard_excludes_drafts`, `Managed_lists_only_own_chapters`, `Commission_detail_has_member_numbers`.
- [ ] **Step 3: Implement**, register the service, build, run both classes. Expected: PASS.

### Task 14: Inquiries

**Files:**
- Create: `Quartermaster.Data/AssemblyMotions/AssemblyMotionInquiryRepository.cs` (register in `AddRepositories`)
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionInquiryService.cs`
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionInquiryDTO.cs`, requests `AssemblyMotionInquiryAskRequest` (MotionId, QuestionMarkdown, ResponseDueAt), `AssemblyMotionInquiryAnswerRequest` (Id, AnswerMarkdown), `AssemblyMotionInquiryIdRequest`
- Create: endpoints `Commission/AssemblyMotionInquiryAskEndpoint.cs` (`POST /api/assemblymotions/motions/{MotionId}/inquiries`), `Commission/AssemblyMotionInquiryCloseEndpoint.cs` (`POST /api/assemblymotions/inquiries/{Id}/close`), `Member/AssemblyMotionInquiryAnswerEndpoint.cs` (`POST /api/assemblymotions/inquiries/{Id}/answer`), validators
- Modify: detail builder (`Inquiries` list and `OpenInquiryCount` for submitters and commission), dashboard and mine (`OpenInquiryCount`, `HasOpenInquiry` filter)
- Test: `AssemblyMotionInquiryServiceTests.cs`, `Integration/AssemblyMotions/AssemblyMotionInquiryEndpointTests.cs`

**Interfaces:**

```csharp
public class AssemblyMotionInquiryRepository {
    public AssemblyMotionInquiry? Get(Guid id)
    public List<AssemblyMotionInquiry> ListForMotion(Guid motionId)               // AskedAt ascending
    public Dictionary<Guid, int> CountOpen(IReadOnlyCollection<Guid> motionIds)   // ClosedAt == null
    public bool HasOpen(Guid motionId)
    public void Insert(AssemblyMotionInquiry inquiry)
    public void Update(AssemblyMotionInquiry inquiry)
}

public class AssemblyMotionInquiryService {
    public AssemblyMotionOutcome Ask(Guid motionId, string questionMarkdown, DateTime responseDueAt, Guid userId, DateTime nowUtc)
    public AssemblyMotionOutcome Answer(Guid inquiryId, Member member, string answerMarkdown, DateTime nowUtc)
    public AssemblyMotionOutcome Close(Guid inquiryId, Guid userId, DateTime nowUtc)
}
```

Rules: ask only for motions with `Number != null`; `responseDueAt >= nowUtc.AddHours(24)` (`error.assembly_motion.inquiry_due_too_soon` "Die Antwortfrist muss mindestens 24 Stunden in der Zukunft liegen."); answer by any current submitter while not closed, a second answer replaces the first (audit both); close by commission; ask and close call `MarkWikiPending`. `AssemblyMotionInquiryDTO`: Id, QuestionHtml, AskedAt, ResponseDueAt, AnswerHtml, AnsweredAt, AnsweredByName, ClosedAt. Inquiries appear only for submitters and the commission. The UI defaults `ResponseDueAt` to now + 2 days (Task 16).

- [ ] **Step 1: Tests**: `Due_less_than_24h_refused`, `Any_submitter_can_answer`, `Non_submitter_cannot_answer` (403), `Answer_after_close_refused`, `Inquiry_hidden_from_eligible_non_submitter` (detail DTO `Inquiries` empty), `Ask_queues_wiki_sync`, `Dashboard_filters_open_inquiries`.
- [ ] **Step 2: Implement**, build, run both classes. Expected: PASS.

### Task 15: Notifications

**Files:**
- Modify: `Quartermaster.Server/Notifications/NotificationTriggers.cs` (10 constants), `NotificationTriggerCatalog.cs` (10 entries)
- Create: `Quartermaster.Server/AssemblyMotions/Notifications/AssemblyMotionNotificationPayload.cs`, `AssemblyMotionRecipientResolver.cs`, `AssemblyMotionNotifier.cs`
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionNotificationModel.cs`
- Modify: `Quartermaster.Data/Templates/SystemTemplateSeeds.cs` (email + telegram per trigger), `Quartermaster.Api/Templates/TemplateModelLookup.cs`, `TemplateMockDataProvider.cs` if the lookup requires mock data for every model (check how `MotionDetailDTO` is handled there)
- Modify: `AssemblyMotionService.cs`, `AssemblyMotionCommissionService.cs`, `AssemblyMotionInquiryService.cs` (enqueue after commit), `AssemblyMotionDeadlineHostedService.cs` + `AssemblyMotionService.SendCollectingReminders(DateTime nowUtc)`
- Modify: `Program.cs` (10 resolver registrations, notifier)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionRecipientResolverTests.cs`, `AssemblyMotionNotificationTests.cs`, `AssemblyMotionNotificationTemplateTests.cs`

**Interfaces:**

```csharp
public record AssemblyMotionNotificationPayload(Guid MotionId, Guid EventChapterId, IReadOnlyList<Guid> MemberIds, bool IncludeCommission);

public class AssemblyMotionRecipientResolver : IRecipientResolver {
    public AssemblyMotionRecipientResolver(string triggerId, DbContext db)
    public string TriggerId { get; }
    public IReadOnlyList<NotificationRecipient> Resolve(object payload)
}

public class AssemblyMotionNotifier {
    public AssemblyMotionNotifier(INotificationDispatchQueue queue, DbContext db)
    public void CoSubmittersReset(AssemblyMotion motion, IReadOnlyList<Guid> removedMemberIds)
    public void CollectingReminder(AssemblyMotion motion, DateTime effectiveDeadlineUtc)
    public void Submitted(AssemblyMotion motion)
    public void Lapsed(AssemblyMotion motion, IReadOnlyList<Guid> submitterMemberIds)
    public void Checked(AssemblyMotion motion, IReadOnlyList<Guid> submitterMemberIds)
    public void InquiryAsked(AssemblyMotion motion, AssemblyMotionInquiry inquiry, IReadOnlyList<Guid> submitterMemberIds)
    public void InquiryAnswered(AssemblyMotion motion, AssemblyMotionInquiry inquiry)
    public void EditedByCommission(AssemblyMotion motion, string changeNote, IReadOnlyList<Guid> submitterMemberIds)
    public void CompetitionDeclared(AssemblyMotion motion, AssemblyMotion otherMotion, IReadOnlyList<Guid> otherSubmitterMemberIds)
    public void Withdrawn(AssemblyMotion motion, IReadOnlyList<Guid> coSubmitterMemberIds)
}
```

Triggers (constant name -> id -> recipients):
- `AssemblyMotionCoSubmitterReset` `assembly_motion_cosubmitter_reset`: removed co-submitters.
- `AssemblyMotionCollectingReminder` `assembly_motion_collecting_reminder`: author.
- `AssemblyMotionSubmitted` `assembly_motion_submitted`: commission.
- `AssemblyMotionLapsed` `assembly_motion_lapsed`: all submitter rows.
- `AssemblyMotionChecked` `assembly_motion_checked`: all submitters.
- `AssemblyMotionInquiryAsked` `assembly_motion_inquiry_asked`: all submitters.
- `AssemblyMotionInquiryAnswered` `assembly_motion_inquiry_answered`: commission.
- `AssemblyMotionEditedByCommission` `assembly_motion_edited_by_commission`: all submitters.
- `AssemblyMotionCompetitionDeclared` `assembly_motion_competition_declared`: submitters of the other motion (fired in Task 17).
- `AssemblyMotionWithdrawn` `assembly_motion_withdrawn`: co-submitters (not the author) + commission.

Resolver: members -> `Member.UserId` -> `User` -> `NotificationRecipient(user.Id, user.Email)`; members without a user or users without email are skipped. With `IncludeCommission`: users holding `assemblymotions_manage` globally, directly on `EventChapterId`, or through a role assigned on `EventChapterId` (copy the grant and role queries of `ChapterPermissionRecipientResolver`, but for the exact chapter only, because the permission does not inherit). Deduplicate by `UserId`.

Model: `["motion"] = AssemblyMotionNotificationModel` with Id, DisplayNumber (or "Entwurf"), Title, TypeName, EventName, StatusLabel (German), CheckNote, ChangeNote, InquiryQuestion, InquiryDueAt (`BerlinTime.FormatDeadline`), Deadline (formatted effective deadline), Path (`/AssemblyMotions/{Id}`; the commission triggers use `/Administration/AssemblyMotions/{Id}`), OtherDisplayNumber, OtherTitle. `TemplateModelLookup`: `id.StartsWith("notifications.assembly_motion_")` -> `"AssemblyMotionNotificationModel"`.

Templates: one email (subject + body) and one telegram body per trigger, German, links as `{{ globals.BaseUrl }}{{ motion.Path }}`. Example:

```
notifications.assembly_motion_cosubmitter_reset.email
Subject: Antrag geändert: {{ motion.Title }}
Body: Hallo,\n\nder Antrag *{{ motion.Title }}* wurde von der antragstellenden Person geändert. Deine Mitantragstellung wurde deshalb zurückgesetzt.\n\nWenn du den geänderten Antrag weiter unterstützen möchtest, tritt ihm bitte erneut bei:\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n
```

Enqueue only after `tx.Commit()`: services collect the calls in a local list of `Action`s and run them after commit.

Collecting reminder: `SendCollectingReminders(nowUtc)`: motions in `CollectingCoSubmitters` with `CollectingReminderSentAt == null` whose effective deadline is in `(nowUtc, nowUtc + 48h]` -> notify the author, set `CollectingReminderSentAt`. Called from the deadline job before `LapseOverdue`.

- [ ] **Step 1: Resolver tests**: `Members_resolve_to_linked_users`, `Member_without_user_is_skipped`, `Commission_exact_chapter_only` (grant on the parent chapter does not count, global grant counts, role `motion_commission` on the chapter counts), `Duplicates_removed`.
- [ ] **Step 2: Notification tests** with a capturing `INotificationDispatchQueue` swapped into the service (find an existing capturing fake with `grep -rn INotificationDispatchQueue Quartermaster.Server.Tests`; create `Fakes/CapturingNotificationQueue.cs` if none exists): `Fifth_join_enqueues_submitted_for_commission`, `Author_edit_enqueues_reset_for_removed_only`, `Reminder_sent_once_within_48h`, `Reminder_not_sent_earlier_than_48h`, `Withdraw_notifies_cosubmitters_and_commission_not_author`, `Rolled_back_action_enqueues_nothing` (an action that fails after the submit step, e.g. by forcing a revision conflict, leaves the queue empty).
- [ ] **Step 3: Template seed test** `AssemblyMotionNotificationTemplateTests`: for each of the 10 trigger ids, `SystemTemplateSeeds` contains `notifications.<id>.email` and `notifications.<id>.telegram`, and each email body renders with `TemplateRenderer.RenderTextAsync` against a sample `AssemblyMotionNotificationModel` without error.
- [ ] **Step 4: Implement**, build, run the three classes plus `AssemblyMotionServiceTests`. Expected: PASS.

### Task 16: Blazor pages for phase 2, audit check, changelog

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/AssemblyMotions/AssemblyMotionPortalList.razor(.cs)` route `/Administration/AssemblyMotionPortals` (managed portals)
- Create: `Quartermaster.Blazor/Pages/Administration/AssemblyMotions/AssemblyMotionDashboard.razor(.cs)` route `/Administration/Events/{EventId:guid}/Motions`
- Create: `Quartermaster.Blazor/Pages/Administration/AssemblyMotions/AssemblyMotionCommissionDetail.razor(.cs)` route `/Administration/AssemblyMotions/{Id:guid}`
- Create: `Quartermaster.Blazor/Components/AssemblyMotions/AssemblyMotionRevisionHistory.razor(.cs)` (revision list, diff between a chosen revision and its predecessor using `TextDiff`, added lines green, removed red)
- Create: `Quartermaster.Blazor/Components/AssemblyMotions/AssemblyMotionInquiries.razor(.cs)` (list; answer form for submitters; ask and close for the commission)
- Modify: `Layout/MainNavBar.razor` (Vorstandsarbeit item "Antragskommission" gated by `AnyChapterPermission="@PermissionIdentifier.ManageAssemblyMotions"`, add the permission to the dropdown's outer gate list)
- Modify: `AssemblyMotionEditor` (edit warning, see below), `AssemblyMotionDetail` (revision history, inquiries, "edit locked" hint with link to the open inquiry), `AssemblyMotionPortal` (show the "Antragskommission" button)
- Modify: i18n, `V4.md`

Edit warning in `AssemblyMotionEditor`: as soon as the editor opens for a motion that is not a draft, a prominent `alert-warning` at the top states the consequence, and saving asks again through `ConfirmDialog` with the same text. Texts (i18n, `{count}` and `{deadline}` as parameters):
- Type needs co-submitters: "Achtung: Sobald du eine Änderung speicherst, ist der Antrag wieder in Überarbeitung und für alle so sichtbar, auch im Wiki. Alle {count} Mitantragsteller werden entfernt und müssen erneut beitreten, bevor die Frist am {deadline} abläuft." (the sentence about removal is left out when there are no co-submitters)
- Type needs only the author: "Achtung: Deine Änderung ist nach dem Speichern sofort für alle sichtbar, auch im Wiki."

Commission detail page: everything from the member detail plus member numbers; formal check form (radio Submitted / Admitted / FormallyInsufficient, note, required for FormallyInsufficient); commission edit (same editor fields + required change note); withdraw on request (`ConfirmDialog`); inquiries (ask with `datetime-local` defaulting to now + 2 days in Berlin time); placeholders for wiki and OpenSlides state that show the values from `Commission` (buttons come in Tasks 24 and 28).

Dashboard: filter bar (type, status, open inquiry, wiki state, search), table with the item fields, link to the commission detail, `Pagination`, warning badge with `FailedWikiSyncCount` when > 0.

Audit sweep: confirm every audit action from the spec's "Audit Log" section that belongs to phases 1-2 is written (portal enabled and changed, released, submitted, edited, withdrawn, lapsed, joined, left, reset, check decisions, inquiries asked/answered/closed). Add missing ones.

Changelog sections:

```
### Antragskommission

Die Antragskommission einer Versammlung arbeitet in einer eigenen Übersicht: Formprüfung mit Begründung, Rückfragen an die Antragsteller mit Antwortfrist, Korrekturen am Antragstext mit Änderungsnotiz und Rückzug auf Wunsch der Antragsteller. Die Rolle "Antragskommission" lässt sich wie andere Rollen für eine Gliederung vergeben.

### Änderungen an Anträgen

Antragsteller können ihre Anträge bis zur Frist überarbeiten. Bei einer Änderung müssen Mitantragsteller erneut beitreten; sie werden darüber benachrichtigt. Jede Fassung bleibt in der Versionsgeschichte mit Gegenüberstellung der Änderungen sichtbar.
```

- [ ] **Step 1: Implement**, build, headless screenshots of the dashboard, commission detail and the revision diff (same method as Task 11).

### Checkpoint 2

- [ ] Build; run `TextDiffTests`, `AssemblyMotionServiceTests`, `AssemblyMotionMemberEndpointTests`, `AssemblyMotionCommissionServiceTests`, `AssemblyMotionCommissionEndpointTests`, `AssemblyMotionInquiryServiceTests`, `AssemblyMotionInquiryEndpointTests`, `AssemblyMotionRecipientResolverTests`, `AssemblyMotionNotificationTests`, `AssemblyMotionNotificationTemplateTests`.
- [ ] Restart the local server. Stop and report to the user.

---

# Phase 3: Competition, Prioritization, Results

### Task 17: Competition

**Files:**
- Create: `Quartermaster.Data/AssemblyMotions/AssemblyMotionCompetitionRepository.cs` (register)
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionCompetitionService.cs`
- Create: requests `AssemblyMotionCompetitionDeclareRequest` (MotionId, OtherMotionId), `AssemblyMotionCompetitionIdRequest`, `AssemblyMotionCompetingFlagRequest` (Id, Value); DTO `AssemblyMotionCompetitionDTO` (Id, OtherMotionId, OtherDisplayNumber, OtherTitle, DeclaredByCommission, CanRemove)
- Create: endpoints `Member/AssemblyMotionCompetitionDeclareEndpoint.cs` (`POST /api/assemblymotions/motions/{MotionId}/competitions`, submitters or commission), `Member/AssemblyMotionCompetitionRemoveEndpoint.cs` (`DELETE /api/assemblymotions/competitions/{Id}`), `Member/AssemblyMotionCompetingFlagEndpoint.cs` (`PUT /api/assemblymotions/motions/{Id}/competing-flag`)
- Modify: `AssemblyMotionPublicView` (public `Competitions`), detail builder (`Competitions` with `CanRemove`), `AssemblyMotionService.Release` (send pending competition notifications of a draft)
- Test: `AssemblyMotionCompetitionServiceTests.cs`, extend `AssemblyMotionPublicEndpointTests.cs`

**Interfaces:**

```csharp
public class AssemblyMotionCompetitionRepository {
    public List<AssemblyMotionCompetition> ListForMotion(Guid motionId)                         // where A or B == motionId
    public Dictionary<Guid, List<AssemblyMotionCompetition>> ListForMotions(IReadOnlyCollection<Guid> motionIds)
    public AssemblyMotionCompetition? Get(Guid id)
    public bool Exists(Guid motionAId, Guid motionBId)
    public void Insert(AssemblyMotionCompetition competition)
    public void Delete(Guid id)
}

public class AssemblyMotionCompetitionService {
    public AssemblyMotionOutcome Declare(Guid motionId, Guid otherMotionId, Guid userId, Member? member, bool asCommission, DateTime nowUtc)
    public AssemblyMotionOutcome Remove(Guid competitionId, Guid userId, Member? member, bool asCommission, DateTime nowUtc)
    public AssemblyMotionOutcome SetCompetingFlag(Guid motionId, bool value, Member? member, bool asCommission, DateTime nowUtc)
    public static (Guid A, Guid B) OrderPair(Guid first, Guid second)
}
```

Rules:
- Declare: same portal; not the same motion; the other motion is `Submitted` or `Admitted`; the declaring motion is `Draft`, `CollectingCoSubmitters`, `Submitted` or `Admitted`; a non-commission caller must be a current submitter of the declaring motion (for a draft: its author); pair unique (`error.assembly_motion.competition_exists`). Stores `DeclaringMotionId = motionId`, `DeclaredByCommission = asCommission`. `MarkWikiPending` on both motions where `WasSubmitted`. Audit `CompetitionDeclared` on both motions. Notification `CompetitionDeclared` to the other motion's submitters, except while the declaring motion is a draft: then `Release` sends it.
- Remove: a link with `DeclaredByCommission` only by the commission; otherwise by a current submitter of `DeclaringMotionId` or the commission. After removal, if a motion with `IsCompetingMotion` has no remaining qualifying link, clear the flag (ruling: the spec requires at least one link for the flag; clearing keeps the data consistent; if wrong, the author sets the flag again). Wiki pending and audit as for declare.
- Qualifying link for the flag: a link to another motion with `Number != null` whose `SubmittedAt` is earlier than this motion's `SubmittedAt`, or any such motion when this motion has no `SubmittedAt` yet.
- SetCompetingFlag: author may set `true` only with a qualifying link and while `CanEditAsAuthor` holds; author may set `false` while `CanEditAsAuthor` holds; the commission may only set `false` (any status). Audit.
- Public competitions: links where the other motion `IsPublic`, showing its display number and title.

- [ ] **Step 1: Tests**: `Declare_is_symmetric` (visible from both motions), `Pair_stored_ordered_and_unique` (declaring B->A after A->B fails), `Declare_against_collecting_motion_refused`, `Commission_link_removable_only_by_commission`, `Submitter_link_removable_by_declaring_submitter_not_by_other_side`, `Flag_requires_earlier_submitted_link`, `Removing_last_link_clears_flag`, `Draft_declaration_notifies_on_release`, `Public_detail_lists_competition_without_names`.
- [ ] **Step 2: Implement**, build, run `AssemblyMotionCompetitionServiceTests`, `AssemblyMotionPublicEndpointTests`. Expected: PASS.

### Task 18: Prioritization and ranking

**Files:**
- Create: `Quartermaster.Data/AssemblyMotions/AssemblyMotionUpvoteRepository.cs` (register)
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionUpvoteService.cs`, `AssemblyMotionRanking.cs`
- Create: DTOs `AssemblyMotionRankingItemDTO` (Rank, Id, DisplayNumber, Title, TypeId, TypeCode, TypeName, TypeSortOrder, UpvoteCount, Status), `AssemblyMotionRankingResponse` (Items, EndsAt)
- Create: endpoints `Member/AssemblyMotionUpvoteEndpoint.cs` (`POST /api/assemblymotions/motions/{Id}/upvote`), `Member/AssemblyMotionUpvoteRemoveEndpoint.cs` (`DELETE /api/assemblymotions/motions/{Id}/upvote`), `Member/AssemblyMotionMyUpvotesEndpoint.cs` (`GET /api/assemblymotions/portals/{PortalId}/my-upvotes` -> list of motion ids), `Public/AssemblyMotionRankingEndpoint.cs` (`GET /api/assemblymotions/portals/{PortalId}/ranking`, anonymous)
- Modify: detail builder (`HasUpvoted`, `CanUpvote`, `UpvoteCount` only after the window), public view (`UpvoteCount` after the window)
- Test: `AssemblyMotionUpvoteServiceTests.cs`, `AssemblyMotionRankingTests.cs`, `Integration/AssemblyMotions/AssemblyMotionRankingEndpointTests.cs`

**Interfaces:**

```csharp
public class AssemblyMotionUpvoteRepository {
    public bool Exists(Guid motionId, Guid memberId)
    public void Insert(AssemblyMotionUpvote upvote)
    public void Delete(Guid motionId, Guid memberId)
    public List<Guid> MotionIdsForMember(Guid portalId, Guid memberId)
    public Dictionary<Guid, int> CountsForPortal(Guid portalId)
}

public class AssemblyMotionUpvoteService {
    public AssemblyMotionOutcome Upvote(Guid motionId, Member member, DateTime nowUtc)
    public AssemblyMotionOutcome Remove(Guid motionId, Member member, DateTime nowUtc)
    public static bool IsWindowOpen(AssemblyMotionPortal portal, DateTime nowUtc)
    public static bool IsRankingPublic(AssemblyMotionPortal portal, DateTime nowUtc)
}

public record AssemblyMotionRankingEntry(AssemblyMotion Motion, AssemblyMotionType Type, int UpvoteCount, int Rank);

public static class AssemblyMotionRanking {
    public static List<AssemblyMotionRankingEntry> Compute(IEnumerable<AssemblyMotion> motions, IReadOnlyDictionary<Guid, AssemblyMotionType> types, IReadOnlyDictionary<Guid, int> counts)
}
```

Rules: window open = `PrioritizationEnabled` and `StartsAt <= now <= EndsAt`; ranking public = enabled and `now > EndsAt` (`error.assembly_motion.ranking_not_available`, 409, before that). Upvote needs eligibility (endpoint), status `Submitted` or `Admitted`, unique per member (a second upvote is a no-op success). `Compute` keeps only `Submitted` and `Admitted` motions, orders by count descending, `SubmittedAt` ascending, type `SortOrder`, `Number`; `Rank` is the 1-based position. Counts are never returned while the window is open or before it, not even to the commission. Upvotes are not audited. The ranking response contains no member data.

- [ ] **Step 1: Tests**: `Upvote_outside_window_refused` (before start, after end), `Upvote_twice_is_noop`, `Upvote_on_collecting_motion_refused`, `Counts_hidden_during_window` (detail and public DTO `UpvoteCount == null` for a commission user), `Ranking_order_and_tie_breaks` (equal counts ordered by earlier `SubmittedAt`, then type order, then number), `Insufficient_motion_drops_out_of_ranking`, `Ranking_endpoint_409_before_end`, `Ranking_response_has_no_names`.
- [ ] **Step 2: Implement**, build, run the three classes. Expected: PASS.

### Task 19: Results

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/AssemblyMotionResultService.cs`
- Create: requests `AssemblyMotionResultRequest` (Id, Result), `AssemblyMotionBulkResultRequest` (PortalId, Items: list of `AssemblyMotionResultItem` MotionId + Result)
- Create: endpoints `Commission/AssemblyMotionResultEndpoint.cs` (`PUT /api/assemblymotions/motions/{Id}/result`), `Commission/AssemblyMotionBulkResultEndpoint.cs` (`PUT /api/assemblymotions/portals/{PortalId}/results`)
- Test: `AssemblyMotionResultServiceTests.cs`

Rules: commission only; the event date must be reached: `ev.EventDate != null && BerlinTime.FromUtc(nowUtc).Date >= BerlinTime.FromUtc(ev.EventDate.Value).Date` (`error.assembly_motion.event_not_reached`); motion must have a number; bulk runs in one transaction and fails as a whole when one item is invalid (error names the display number); each change audited with `LogFieldChange(..., "Result", old, new)` and `MarkWikiPending`.

- [ ] **Step 1: Tests**: `Result_before_event_date_refused`, `Result_on_event_day_allowed`, `Bulk_is_atomic`, `Result_requires_number`.
- [ ] **Step 2: Implement**, build, run. Expected: PASS.

### Task 20: Blazor pages for phase 3, changelog

**Files:**
- Create: `Quartermaster.Blazor/Pages/AssemblyMotions/AssemblyMotionRanking.razor(.cs)` route `/Events/{EventId:guid}/Motions/Ranking` (two views via tabs: overall list, grouped by type in the same order)
- Create: `Quartermaster.Blazor/Pages/Administration/AssemblyMotions/AssemblyMotionResults.razor(.cs)` route `/Administration/Events/{EventId:guid}/Motions/Results` (table of numbered motions with a result select per row, one save button using the bulk endpoint)
- Create: `Quartermaster.Blazor/Components/AssemblyMotions/AssemblyMotionCompetitions.razor(.cs)` (list with remove buttons from `CanRemove`; declare form: search the portal's public motions by number or title and pick one; competing flag checkbox for the author with explanation of the competing deadline)
- Modify: `AssemblyMotionDetail` (competition component, upvote toggle during the window with "Du hast diesen Antrag priorisiert", count after the window), `AssemblyMotionPortal` (link to the ranking after the window; marker on motions the member upvoted during the window via `my-upvotes`), `AssemblyMotionCommissionDetail` (competition component with commission rights, clear competing flag, result select), `AssemblyMotionDashboard` (link to results page)
- Modify: i18n, `V4.md`

Changelog sections:

```
### Konkurrierende Anträge

Anträge, die einander ausschließen, lassen sich als konkurrierend verknüpfen. Die Verknüpfung ist bei beiden Anträgen öffentlich sichtbar. Für konkurrierende Anträge kann eine spätere Frist gelten.

### Priorisierung

Wenn die Versammlung es vorsieht, priorisieren Mitglieder in einem festgelegten Zeitraum die Anträge, die ihnen wichtig sind. Während des Zeitraums sieht niemand Zwischenstände; danach bestimmt das Ergebnis die Reihenfolge der Anträge und ist öffentlich einsehbar.

### Ergebnisse

Nach der Versammlung trägt die Antragskommission die Ergebnisse der Abstimmungen ein. Sie sind am Antrag öffentlich sichtbar.
```

- [ ] **Step 1: Implement**, build, headless screenshots of the ranking page (both views) and the results table.

### Checkpoint 3

- [ ] Build; run `AssemblyMotionCompetitionServiceTests`, `AssemblyMotionPublicEndpointTests`, `AssemblyMotionUpvoteServiceTests`, `AssemblyMotionRankingTests`, `AssemblyMotionRankingEndpointTests`, `AssemblyMotionResultServiceTests`, `AssemblyMotionServiceTests`.
- [ ] Restart the local server. Stop and report to the user.

---

# Phase 4: Wiki Mirror

### Task 21: Markdown to MediaWiki converter

**Superseded during implementation (review findings):** `|` is emitted as `&#124;` everywhere and never as `{{!}}`; `!`, `"`, `=`, `*`, `#`, `;`, `:` are entities too; input is NFC-normalized and a combining character after emitted markup is a numeric reference; control characters are stripped; `ISBN`/`RFC`/`PMID` are defused in a final pass; `pre` is emitted without an inner `code`; `thead`/`tbody` are dropped; `MotionMarkdownProblem` has `TooDeeplyNested` and `TooLong` (100,000 characters). Where the text below says `{{!}}`, read `&#124;`.

**Files:**
- Create: `Quartermaster.Rendering/MotionWikiConverter.cs`
- Modify: `Quartermaster.Rendering/Quartermaster.Rendering.csproj` only if `AngleSharp` (already a dependency of `HtmlSanitizer`) is not resolvable directly; then add an explicit `PackageReference` with the version `HtmlSanitizer` pulls in
- Test: `Quartermaster.Server.Tests/AssemblyMotions/MotionWikiConverterTests.cs`

**Interfaces:**
- Consumes: `MarkdownService.ToHtml(markdown, SanitizationProfile.Standard, MarkdownFlavor.Motion)` and `MotionMarkdown` from Task 4.
- Produces:

```csharp
public static class MotionWikiConverter {
    public static string ToWikitext(string markdown)     // restricted Markdown -> MediaWiki text, safe inside a template parameter
    public static string EscapeInline(string text)       // plain single-line text (title, keywords, ...) -> safe wikitext
}
```

Approach: render the Markdown to sanitized HTML with the motion flavour, parse that HTML with AngleSharp's `HtmlParser`, and walk the DOM. MediaWiki accepts these HTML tags directly, so the output is mostly HTML; only links need wiki syntax.

- Allowed elements, emitted as the same tag without any attributes: `p`, `br`, `strong`, `em`, `del`, `s`, `ul`, `ol`, `li`, `h1` to `h6`, `blockquote`, `pre`, `code`, `hr`, `table`, `thead`, `tbody`, `tr`, `th`, `td`. Block elements start on a new line, so no output line ever starts with member text (this keeps MediaWiki's line-start markers `*`, `#`, `:`, `;`, `=`, space out of reach).
- `a` with an `http`, `https` or `mailto` href -> `[` + URL with space, `[`, `]`, `|`, `{`, `}`, `<`, `>`, `"`, `'` percent-encoded + ` ` + converted children + `]`; any other href -> converted children only.
- Every other element -> its converted children (tag dropped).
- Text nodes -> `EscapeText`, with newlines replaced by a space outside `pre`.

`EscapeText` (ruling: entities instead of `<nowiki>` wrapping, because a member can type `</nowiki>` and partial sequences make wrapping fragile; the spec's goal, no injection, is met either way):

```csharp
private static string EscapeText(string text) {
    var sb = new StringBuilder(text.Length);
    foreach (var c in text) {
        switch (c) {
            case '&':
                sb.Append("&amp;");
                break;
            case '|':
                sb.Append("{{!}}");
                break;
            case '{':
                sb.Append("&#123;");
                break;
            case '}':
                sb.Append("&#125;");
                break;
            case '[':
                sb.Append("&#91;");
                break;
            case ']':
                sb.Append("&#93;");
                break;
            case '<':
                sb.Append("&lt;");
                break;
            case '>':
                sb.Append("&gt;");
                break;
            case '~':
                sb.Append("&#126;");
                break;
            case '\'':
                sb.Append("&#39;");
                break;
            case '_':
                sb.Append("&#95;");
                break;
            default:
                sb.Append(c);
                break;
        }
    }
    return sb.ToString();
}
```

AngleSharp has already decoded entities in text nodes, so escaping `&` again keeps a member-typed `&#124;` literal. `{{!}}` is emitted by us after escaping and cannot come from member text. `_` stops `__NOTOC__`-style magic words, `'` stops bold/italic toggles, `~` stops signatures. `EscapeInline` = `EscapeText` of the trimmed text with newlines replaced by spaces.

- [ ] **Step 1: Tests** (exact expected strings):
  - Formatting: `**fett** und *kursiv*` -> `<p><strong>fett</strong> und <em>kursiv</em></p>`; `~~alt~~` -> `<p><del>alt</del></p>`; `# Titel` -> `<h1>Titel</h1>`; nested list -> `<ul>` with a nested `<ul>` inside the `<li>`; `[Satzung](<https://example.org/a b>)` -> `<p>[https://example.org/a%20b Satzung]</p>`; a pipe table -> `<table>`/`<tr>`/`<th>`/`<td>` without attributes, cell text with `|` escaped to `{{!}}`; `` `x|y&z` `` -> `<p><code>x{{!}}y&amp;z</code></p>`; fenced code -> `<pre><code>...</code></pre>` with entities.
  - Hostile (each asserts the output contains no `|` outside our `{{!}}`, no `{{` or `}}` except as part of `{{!}}`, no `[[`, `]]`, `<nowiki`, `</nowiki`, `<ref`, `__`, `~~~`): `a | text=injected`; `}}{{Löschantrag}}`; `{{subst:Vorlage}}`; `</nowiki>{{X}}`; `[[Kategorie:Spam]]`; `<ref>x</ref>` (raw HTML, rejected by the validator but must still be neutral here); `__NOTOC__`; `~~~~`; `''''`; `<div onclick="x">{{x}}</div>`; `[link](javascript:alert(1))` (label only, no link).
  - `Attributes_are_dropped`: a right-aligned table column (`|---:|`) yields `<td>` without `style`.
- [ ] **Step 2: Implement**, build, run `MotionWikiConverterTests`. Expected: PASS.

### Task 22: Wiki page rendering, default templates, status mapping

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Wiki/AssemblyMotionWikiStatus.cs`, `AssemblyMotionWikiTemplates.cs`, `AssemblyMotionWikiRenderer.cs`, `AssemblyMotionWikiPage.cs`
- Modify: `AssemblyMotionPortalRepository.Create` / `AssemblyMotionDefaultTypes` (seed `WikiTemplate` with `AssemblyMotionWikiTemplates.DefaultFor(code)`; the method lives in Server, so pass the template text into `AssemblyMotionDefaultTypes.Create(portalId, Func<string, string> templateFor)`)
- Test: `AssemblyMotionWikiRendererTests.cs`

**Interfaces:**

```csharp
public static class AssemblyMotionWikiStatus {
    public static int CheckIcon(AssemblyMotion motion, bool hasOpenInquiry)
    public static int VoteResult(AssemblyMotion motion)
}

public static class AssemblyMotionWikiTemplates {
    public static string DefaultFor(string typeCode)
}

public static class AssemblyMotionWikiPage {
    public static string Title(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion)   // $"{portal.WikiPagePrefix}/{AssemblyMotionNumber.Format(type.Code, motion.Number!.Value)}"
}

public record AssemblyMotionWikiInput(AssemblyMotionPortal Portal, AssemblyMotionType Type, AssemblyMotion Motion,
    bool HasOpenInquiry, IReadOnlyList<string> CompetingDisplayNumbers, string PortalUrl, string BotUserName);

public static class AssemblyMotionWikiRenderer {
    public static Task<string> RenderAsync(AssemblyMotionWikiInput input)   // throws InvalidOperationException with the Fluid error on a broken template
}
```

Status mapping (from the spec):
- `CheckIcon`: `Withdrawn` or `Lapsed` -> 6; `Admitted` -> 2; `FormallyInsufficient` -> 5; otherwise (`Submitted`, or in revision; the wiki templates have no value for revision, the notice above the template call shows it) -> 7 with an open inquiry, else 1.
- `VoteResult`: `Withdrawn` or `Lapsed` -> 4; `FormallyInsufficient` -> 8; `Result` `Accepted` -> 2, `Rejected` -> 3, `AcceptedModified` -> 5; otherwise 1.

Model (a `Dictionary<string, object>` of `Dictionary<string, object>` values, so Fluid member access needs no registration): `assembly.Label`, `type.Code`, `type.Name`, `motion.Number` (display number), `motion.Title`, `motion.Section`, `motion.Text`, `motion.Reason`, `motion.Summary`, `motion.Keywords`, `motion.PriorDiscussion`, `motion.CompetingNumbers` (comma separated), `motion.CheckIcon`, `motion.VoteResult`, `motion.PortalUrl`, `motion.InRevision` (bool), `bot.UserName`. Text and reason come from `MotionWikiConverter.ToWikitext(motion.TextMarkdown / ReasonMarkdown)`; title, section, summary, keywords, prior discussion from `MotionWikiConverter.EscapeInline` of the motion fields; the label is escaped too. Render with `TemplateRenderer.RenderTextAsync`.

A type with an empty `WikiTemplate` renders with `DefaultFor(type.Code)`.

Default templates: Fluid treats `{{` as an output tag, so the wiki template braces are emitted as Fluid string literals. `DefaultFor`: `SÄA` -> `Satzungsänderungsantrag BPT`; `GP`, `WP` -> `Programmantrag BPT`; everything else -> `Sonstiger Antrag BPT`, with `|artantrag={{ type.Name }}` added for the last family. Template for SÄA:

```
{% if motion.InRevision %}'''Dieser Antrag wird überarbeitet und sucht erneut Mitantragsteller. Aktueller Stand im Antragsportal: {{ motion.PortalUrl }}'''

{% endif %}{{ "{{" }}Satzungsänderungsantrag BPT
|bundesparteitag={{ assembly.Label }}
|autor=Im Antragsportal einsehbar (Login erforderlich): {{ motion.PortalUrl }}
|antragstyp={{ type.Name }}
|antragsgruppe={{ motion.Section }}
|titel={{ motion.Title }}
|text={{ motion.Text }}
|begruendung={{ motion.Reason }}
|diskussionVorher={{ motion.PriorDiscussion }}
|schlagworte={{ motion.Keywords }}
|zusammenfassung={{ motion.Summary }}
|konkurrenz={{ motion.CompetingNumbers }}
|prüficon={{ motion.CheckIcon }}
|abstimmung={{ motion.VoteResult }}
|wikiBenutzer={{ bot.UserName }}
{{ "}}" }}
```

No names and no member numbers in the model at all (the spec omits `antragstellerID` and `unterstützer`).

- [ ] **Step 1: Tests**: one test per row of both status tables; `Collecting_with_number_maps_like_submitted`; `Default_template_renders_template_call` (output starts with `{{Satzungsänderungsantrag BPT` (when not in revision) and ends with `}}`); `Values_are_not_html_encoded` (a title `A & B` appears as `A & B`, which pins the Fluid encoder); `In_revision_motion_renders_notice_and_current_text`; `Rendered_page_contains_no_member_names` (seed a member named `Zebulon Quaxwell` as author; the rendered string does not contain either name); `Hostile_title_cannot_close_template` (title `}}{{X` renders escaped); `Empty_template_falls_back_to_default`; `Title_builds_from_prefix_and_number` (`Antrag:Bundesparteitag 2027.1/Antragsportal/SÄA001`).
- [ ] **Step 2: Implement**, build, run `AssemblyMotionWikiRendererTests`, `AssemblyMotionRepositoryTests`. Expected: PASS.

### Task 23: Wiki bot client

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Wiki/WikiBotSettings.cs` (pattern `KeycloakSettings.ReadFrom(OptionRepository)`), `IWikiBotClient.cs`, `HttpWikiBotClient.cs`, `WikiBotException.cs`
- Modify: `Program.cs` (singleton registration with its own handler, scoped settings)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/HttpWikiBotClientTests.cs` (+ `Fakes/RecordingHttpMessageHandler.cs` if no reusable one exists; check `grep -rn HttpMessageHandler Quartermaster.Server.Tests`)

**Interfaces:**

```csharp
public record WikiBotSettings(string ApiUrl, string Username, string Password, string ProtectionLevel) {
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiUrl) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    public static WikiBotSettings ReadFrom(OptionRepository options)
}

public interface IWikiBotClient {
    Task LoginAsync(WikiBotSettings settings, CancellationToken ct);
    Task<string?> GetContentAsync(WikiBotSettings settings, string title, CancellationToken ct);     // null when the page does not exist
    Task EditAsync(WikiBotSettings settings, string title, string text, string summary, CancellationToken ct);
    Task ProtectAsync(WikiBotSettings settings, string title, CancellationToken ct);
}
```

`HttpWikiBotClient` is a singleton: constructor `HttpWikiBotClient(HttpMessageHandler handler)`, creates `new HttpClient(handler)`; registration passes `new SocketsHttpHandler { UseCookies = true, CookieContainer = new CookieContainer(), PooledConnectionLifetime = TimeSpan.FromMinutes(15) }` (the wiki session lives in the cookie container). It keeps the CSRF token and the logged-in user name in fields guarded by a `SemaphoreSlim(1, 1)`; a settings change (different user or URL) forces a new login. Every request sends `User-Agent: Quartermaster/4 (+https://piratenpartei.de)` (same reason as `HttpWikiClient`: the wiki's bot filter challenges browser-like agents) and `format=json&formatversion=2`.

Flow:
- Login: `GET action=query&meta=tokens&type=login` -> `query.tokens.logintoken`; `POST action=login` form `lgname`, `lgpassword`, `lgtoken` -> `login.result == "Success"` else `WikiBotException("Anmeldung am Wiki fehlgeschlagen: <reason>")`; `GET action=query&meta=tokens` -> `query.tokens.csrftoken`.
- GetContent: `GET action=query&prop=revisions&rvprop=content&rvslots=main&titles=<title>` -> `query.pages[0].missing == true` -> null, else `revisions[0].slots.main.content`.
- Edit: `POST action=edit` form `title`, `text`, `summary`, `bot=1`, `assert=user`, `token` -> `edit.result == "Success"`.
- Protect: `POST action=protect` form `title`, `protections=edit=<level>|move=<level>`, `expiry=infinite|infinite`, `assert=user`, `token`.
- A response with `error.code` in `badtoken`, `assertuserfailed`, `notloggedin` -> log in again once and retry the call; a second failure or any other `error` -> `WikiBotException` with `error.info`. A body that starts with `<` (bot filter page) -> `WikiBotException("Das Wiki hat die Anfrage blockiert (HTML statt JSON).")`.

- [ ] **Step 1: Tests** with a recording handler that serves canned JSON per `action`: `Login_runs_token_login_csrf_sequence`, `Every_request_sends_user_agent`, `Missing_page_returns_null`, `Edit_sends_bot_and_assert_user`, `Protect_sends_level_for_edit_and_move`, `Badtoken_triggers_one_relogin_and_retry`, `Second_failure_throws`, `Html_response_throws_blocked`, `Changed_settings_force_new_login`.
- [ ] **Step 2: Implement**, build, run `HttpWikiBotClientTests`. Expected: PASS.

### Task 24: Sync service, background jobs, reconcile

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Wiki/AssemblyMotionWikiSync.cs`, `AssemblyMotionWikiReconcile.cs`, `AssemblyMotionWikiSyncHostedService.cs`, `AssemblyMotionWikiReconcileHostedService.cs`
- Modify: `AssemblyMotionRepository.cs` (`ListWikiDue(DateTime nowUtc, int limit)`, `CompleteWikiSync(...)` conditional update, `FailWikiSync(...)`)
- Modify: `Program.cs`
- Create: `Quartermaster.Server.Tests/AssemblyMotions/Fakes/FakeWikiBotClient.cs`
- Test: `AssemblyMotionWikiSyncTests.cs`

**Interfaces:**

```csharp
public class AssemblyMotionWikiSync {
    public AssemblyMotionWikiSync(DbContext db, AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        AssemblyMotionInquiryRepository inquiries, AssemblyMotionCompetitionRepository competitions,
        IWikiBotClient wiki, OptionRepository options, AuditLogRepository auditLog, ILogger<AssemblyMotionWikiSync> logger)
    public Task<int> SyncDueAsync(DateTime nowUtc, CancellationToken ct)
    public Task<bool> SyncOneAsync(Guid motionId, DateTime nowUtc, CancellationToken ct)
}

public class AssemblyMotionWikiReconcile {
    public Task<int> RunAsync(DateTime nowUtc, CancellationToken ct)    // returns the number of motions queued
}
```

`ListWikiDue`: `WikiSyncState` is `Pending`, or `Failed` with `WikiNextAttemptAt <= now`, or `ProtectionFailed` with `WikiNextAttemptAt <= now`; `Number != null`; the portal has `WikiMirrorEnabled`; ordered by `WikiSyncRequestedAt`; `limit` 20.

`SyncOneAsync` per motion:
1. Settings not configured -> leave the motion untouched, log a warning once per run, return false.
2. Load portal, type, open inquiry flag, competing display numbers (other motion has a number), portal URL (`{globals BaseUrl}/AssemblyMotions/{id}`; read the base URL the way `NotificationTemplateGlobals` does), render, `newHash` = lowercase hex SHA-256 of the UTF-8 text. Remember the loaded `WikiSyncRequestedAt`.
3. `current = GetContentAsync(title)`.
   - null -> `EditAsync(title, text, "Antragsportal: Antrag angelegt (Revision n)")`, then `ProtectAsync`; protection exception -> state `ProtectionFailed`, `WikiLastError` = message, `WikiNextAttemptAt = now + 1h`.
   - exists and `Hash(current) == newHash` -> nothing to write.
   - exists and `WikiSyncedHash != null && Hash(current) != WikiSyncedHash` -> audit `Log("AssemblyMotion", id, "WikiManualEditOverwritten", "PageTitle", null, title)`, then edit.
   - exists otherwise -> edit with summary "Antragsportal: <reason> (Revision n)" where reason is "Status aktualisiert" (keep it generic; the spec example "Revision 3 eingereicht" is used when `WikiSyncedRevision != Revision`).
   - Previous state `ProtectionFailed` -> try `ProtectAsync` again after the write.
4. Success -> `CompleteWikiSync(motionId, loadedRequestedAt, state, revision, hash, now)`: `UPDATE ... SET WikiSyncState, WikiSyncedRevision, WikiSyncedHash, WikiLastSyncedAt, WikiLastError = null, WikiFailureCount = 0 WHERE Id = @id AND WikiSyncRequestedAt <=> @loaded` (null-safe). When no row matched, a newer change arrived during the sync: leave it `Pending`.
5. Exception (`WikiBotException`, `HttpRequestException`, template error) -> `FailWikiSync`: `Failed`, `WikiLastError`, `WikiFailureCount++`, `WikiNextAttemptAt = now + min(2^count minutes, 6 h)`; audit `WikiSyncFailed` only when the count becomes 1.

Reconcile: portals with `WikiMirrorEnabled` whose event status is not `Archived`; for every motion with a number in `Synced` state: `GetContentAsync`; missing page or `Hash(content) != WikiSyncedHash` -> `MarkWikiPending`. Errors are logged and skipped.

Hosted services: sync every minute (`SyncDueAsync`), reconcile once per day, both following the `RetentionAnonymizationHostedService` loop with a scope per run.

`FakeWikiBotClient`: static `Dictionary<string, string> Pages`, `List<string> Protected`, `List<(string Title, string Summary)> Edits`, flags `FailNextEdit`, `FailProtect`, `Configured`, `Reset()`; tests run `[NotInParallel("FakeWikiBotClient")]`.

- [ ] **Step 1: Tests** (`RepositoryTestBase`, service constructed directly with the fake; options seeded with wiki settings):
  - `First_sync_creates_and_protects_page`.
  - `Protection_failure_sets_protection_failed_and_retries_later`.
  - `Unchanged_content_is_not_rewritten` (second sync with identical render: no new edit).
  - `Manual_edit_is_overwritten_and_audited` (change `Pages[title]` in the fake between syncs).
  - `Failure_backs_off_and_retries` (no attempt before `WikiNextAttemptAt`, attempt after).
  - `Disabled_mirror_is_ignored`.
  - `Unconfigured_settings_leave_pending`.
  - `Edited_motion_updates_wiki_to_in_revision` (author edit through `UpdateByAuthor` on a submitted five-submitter motion: the next sync writes the new text and the revision notice).
  - `Change_during_sync_stays_pending` (bump `WikiSyncRequestedAt` from inside the fake's edit callback; afterwards state is `Pending`).
  - `Reconcile_queues_missing_and_mismatched_pages`, `Reconcile_skips_archived_events`.
  - `Withdrawn_motion_page_shows_icon_6` (rendered `prüficon=6`).
- [ ] **Step 2: Implement**, register services and both hosted services, build, run `AssemblyMotionWikiSyncTests`. Expected: PASS.

### Task 25: Wiki settings validation, resync endpoints, UI, docs

**Files:**
- Create: endpoints `Portal/AssemblyMotionWikiValidateEndpoint.cs` (`POST /api/assemblymotions/portals/{PortalId}/wiki/validate`, `events_edit`, returns `AssemblyMotionConnectionCheckDTO { bool Ok, string Message }`), `Commission/AssemblyMotionWikiResyncEndpoint.cs` (`POST /api/assemblymotions/motions/{Id}/wiki/resync`), `Commission/AssemblyMotionWikiResyncAllEndpoint.cs` (`POST /api/assemblymotions/portals/{PortalId}/wiki/resync`)
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionConnectionCheckDTO.cs`
- Modify: `AssemblyMotionPortalUpdateEndpoint.cs` (switching `WikiMirrorEnabled` from false to true runs `LoginAsync`; failure -> 422 `error.assembly_motion.wiki_login_failed` "Anmeldung am Wiki fehlgeschlagen: {reason}"; success -> `MarkWikiPending` for every numbered motion of the portal)
- Modify: Blazor `AssemblyMotionPortalSettings` ("Verbindung prüfen" next to the wiki fields), `AssemblyMotionDashboard` (wiki state column with badge, "Alle neu synchronisieren"), `AssemblyMotionCommissionDetail` (state, last sync, error, "Neu synchronisieren")
- Modify: `Quartermaster.Documentation/Configuration.md` (section "Wiki mirror for the motion portal": the four `wiki.*` options, the prerequisites from the spec, the User-Agent note), i18n, `V4.md`
- Test: `Integration/AssemblyMotions/AssemblyMotionWikiEndpointTests.cs` (fake client swapped in like `AccreditationEndpointTests`)

Resync one or all: `MarkWikiPending` (only numbered motions of mirrored portals), audit `WikiResyncRequested`. Validate endpoint: options not configured -> `Ok = false` with "Die Wiki-Zugangsdaten sind nicht hinterlegt."; login exception -> `Ok = false` with the message.

Changelog section:

```
### Anträge im Wiki

Jeder eingereichte Antrag erscheint automatisch als geschützte Seite im Piratenwiki und wird bei jeder Änderung im Antragsportal aktualisiert. Namen von Antragstellern werden dort nicht veröffentlicht.
```

- [ ] **Step 1: Tests**: `Validate_reports_unconfigured`, `Validate_reports_login_failure`, `Enabling_mirror_with_bad_credentials_refused`, `Enabling_mirror_queues_numbered_motions`, `Resync_requires_commission`.
- [ ] **Step 2: Implement**, build, run the test class, headless screenshot of the dashboard with wiki states.

### Checkpoint 4

- [ ] Build; run `MotionWikiConverterTests`, `AssemblyMotionWikiRendererTests`, `HttpWikiBotClientTests`, `AssemblyMotionWikiSyncTests`, `AssemblyMotionWikiEndpointTests`, `AssemblyMotionRepositoryTests`.
- [ ] Restart the local server (wiki options stay empty on the VM, so nothing is written to the real wiki). Stop and report to the user, including the list of prerequisites for the first real run (bot account, bot password with edit, create and protect rights, membership in the group allowed to edit at the `Antrag` level).

---

# Phase 5: OpenSlides Push and JSON Export

### Task 26: OpenSlides client

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/OpenSlides/OpenSlidesSettings.cs`, `IOpenSlidesClient.cs`, `HttpOpenSlidesClient.cs`, `OpenSlidesException.cs`, `OpenSlidesMotionCreate.cs`, `OpenSlidesMotionUpdate.cs` (the two records may share one file as pure data types)
- Modify: `Program.cs` (singleton client with `IHttpClientFactory.CreateClient()`, scoped settings)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/HttpOpenSlidesClientTests.cs`

**Interfaces:**

```csharp
public record OpenSlidesSettings(string Url, string Username, string Password) {
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    public static OpenSlidesSettings ReadFrom(OptionRepository options)
}

public record OpenSlidesMotionCreate(int MeetingId, string Title, string Number, string TextHtml, string? ReasonHtml, int CategoryId, string AdditionalSubmitter);
public record OpenSlidesMotionUpdate(int MotionId, string Title, string TextHtml, string? ReasonHtml, string AdditionalSubmitter);

public interface IOpenSlidesClient {
    Task LoginAsync(OpenSlidesSettings settings, CancellationToken ct);
    Task<int> CreateCategoryAsync(OpenSlidesSettings settings, int meetingId, string name, string prefix, CancellationToken ct);
    Task<int> CreateMotionAsync(OpenSlidesSettings settings, OpenSlidesMotionCreate motion, CancellationToken ct);
    Task UpdateMotionAsync(OpenSlidesSettings settings, OpenSlidesMotionUpdate motion, CancellationToken ct);
}
```

Protocol (verified against OpenSlides 4.3.2 backend and auth service):
- Login: `POST {Url}/system/auth/login`, JSON `{"username": ..., "password": ...}`. Access token = response header `Authentication`; refresh token = `refreshId` cookie from `Set-Cookie`. Store both in fields guarded by a `SemaphoreSlim(1, 1)`; a different URL or user forces a new login. Login without the header -> `OpenSlidesException("Anmeldung an OpenSlides fehlgeschlagen.")`.
- Action: `POST {Url}/system/action/handle_request`, headers `Authentication: <token>` and `Cookie: refreshId=<value>`, body `[{"action": "<name>", "data": [ { ... } ]}]` with snake_case keys written explicitly (`Dictionary<string, object?>`), nulls omitted.
  - `motion_category.create`: `meeting_id`, `name`, `prefix`.
  - `motion.create`: `meeting_id`, `title`, `number`, `text`, `reason` (omit when null or empty), `category_id`, `additional_submitter`.
  - `motion.update`: `id`, `title`, `text`, `reason` (omit when null or empty), `additional_submitter`.
- Response JSON `{ "success": bool, "message": string, "results": [[{ "id": n, ... }]] }`. `success == false` -> `OpenSlidesException(message)`. Created id = `results[0][0].id`.
- A response carrying an `Authentication` header replaces the stored access token.
- HTTP 401 -> log in again once and retry; a second 401 -> `OpenSlidesException("OpenSlides hat die Anmeldung abgelehnt.")`. Other non-success status codes -> `OpenSlidesException` with status code and body (truncated to 500 chars).

- [ ] **Step 1: Tests** with a recording handler: `Login_reads_header_and_cookie`, `Action_sends_token_and_cookie`, `Create_motion_body_uses_snake_case_and_omits_empty_reason`, `Created_id_read_from_results`, `New_authentication_header_replaces_token`, `Unauthorized_triggers_one_relogin`, `Second_unauthorized_throws`, `Success_false_throws_with_message`.
- [ ] **Step 2: Implement**, build, run `HttpOpenSlidesClientTests`. Expected: PASS.

### Task 27: Push service and endpoints

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/OpenSlides/AssemblyMotionOpenSlidesPush.cs`
- Create: `Quartermaster.Api/AssemblyMotions/OpenSlidesPushDTOs.cs` (`OpenSlidesPushRequest` PortalId, IncludeSubmitted; `OpenSlidesPushResultDTO` Created, Updated, Skipped, Failures: list of `OpenSlidesPushFailureDTO` DisplayNumber, Error)
- Create: endpoints `Commission/AssemblyMotionOpenSlidesPushEndpoint.cs` (`POST /api/assemblymotions/portals/{PortalId}/openslides/push`), `Portal/AssemblyMotionOpenSlidesValidateEndpoint.cs` (`POST /api/assemblymotions/portals/{PortalId}/openslides/validate`, `events_edit`, returns `AssemblyMotionConnectionCheckDTO`)
- Create: `Quartermaster.Server.Tests/AssemblyMotions/Fakes/FakeOpenSlidesClient.cs`
- Test: `AssemblyMotionOpenSlidesPushTests.cs`, `Integration/AssemblyMotions/AssemblyMotionOpenSlidesEndpointTests.cs`

**Interfaces:**

```csharp
public class AssemblyMotionOpenSlidesPush {
    public AssemblyMotionOpenSlidesPush(DbContext db, AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        AssemblyMotionUpvoteRepository upvotes, IOpenSlidesClient client, OptionRepository options, AuditLogRepository auditLog)
    public Task<AssemblyMotionOutcome> ValidateAsync(AssemblyMotionPortal portal, CancellationToken ct)
    public Task<OpenSlidesPushResultDTO> PushAsync(AssemblyMotionPortal portal, bool includeSubmitted, Guid userId, DateTime nowUtc, CancellationToken ct)
}
```

Rules (spec "OpenSlides Push"):
- Preconditions checked by the endpoint: commission; settings configured (`error.assembly_motion.openslides_not_configured`, 409); `portal.OpenSlidesMeetingId != null` (`error.assembly_motion.openslides_meeting_missing`, 409).
- Selection: status `Admitted`, plus `Submitted` when `includeSubmitted`. Order: `AssemblyMotionRanking.Compute` when the ranking is public (`AssemblyMotionUpvoteService.IsRankingPublic`), otherwise type `SortOrder`, then `Number`.
- Content from the motion's current fields: title, `TextHtml`, `ReasonHtml` (null when empty); `Number` = display number; `AdditionalSubmitter` = submitter names (author first, `"Ehemaliges Mitglied"` for anonymized) joined with `", "`.
- Category per type: when `type.OpenSlidesCategoryId == null`, create it (`name` = type name, `prefix` = type code) and store the id immediately. A failure here marks all motions of that type as failed with the error and continues with the other types.
- Per motion, each in its own try/catch:
  - `OpenSlidesMotionId == null` -> create; store `OpenSlidesMotionId` in its own update right after the call returns, then `OpenSlidesPushedRevision = Revision`, `OpenSlidesPushedAt`, `OpenSlidesLastError = null`; `Created++`.
  - `Revision > OpenSlidesPushedRevision` -> update; same bookkeeping; `Updated++`.
  - else `Skipped++`.
  - exception (`OpenSlidesException`, `HttpRequestException`) -> `OpenSlidesLastError = message`, add to `Failures`.
- Audit once per push on the portal: `Log("AssemblyMotionPortal", portal.Id, "OpenSlidesPushed", null, null, "created=x updated=y skipped=z failed=w")`.
- Ruling: the push runs synchronously in the request; a portal has at most a few hundred motions, and the commission wants the result list immediately.

- [ ] **Step 1: Service tests** with `FakeOpenSlidesClient` (static lists of created categories and motions, incrementing ids, `FailCreateFor(string number)`, `Reset()`, `[NotInParallel("FakeOpenSlidesClient")]`):
  - `Creates_category_once_per_type` (two pushes, one category per type).
  - `Second_push_skips_unchanged`.
  - `Commission_edit_triggers_update_on_next_push`.
  - `Only_admitted_unless_submitted_included`; `Withdrawn_never_pushed`.
  - `One_failure_does_not_stop_others` (result lists the failure with display number).
  - `Repush_after_partial_failure_creates_no_duplicates` (first push fails for SÄA002 only; second push creates exactly SÄA002 and skips the rest; the fake holds exactly one motion per number).
  - `Order_follows_ranking_after_window` (creation order in the fake equals ranking order).
  - `Submitters_sent_as_names_author_first`.
- [ ] **Step 2: Endpoint tests**: `Push_requires_commission`, `Push_without_meeting_id_conflicts`, `Push_unconfigured_conflicts`, `Validate_reports_login_failure`.
- [ ] **Step 3: Implement**, register, build, run both classes. Expected: PASS.

### Task 28: JSON export

**Files:**
- Create: `Quartermaster.Api/AssemblyMotions/AssemblyMotionExportDTOs.cs` (`AssemblyMotionExportDTO` with Portal (EventName, AssemblyLabel), ExportedAt, Motions: list of `AssemblyMotionExportItemDTO`)
- Create: `Quartermaster.Server/AssemblyMotions/Endpoints/Commission/AssemblyMotionExportEndpoint.cs` (`GET /api/assemblymotions/portals/{PortalId}/export`)
- Test: `Integration/AssemblyMotions/AssemblyMotionExportEndpointTests.cs`

Content: all motions with a number (every status from `Submitted` on, including withdrawn and lapsed, with their status), in the same order as the push. Item fields: DisplayNumber, TypeCode, TypeName, RequiredMajority, Title, Section, TextMarkdown, TextHtml, ReasonMarkdown, ReasonHtml, Summary, Keywords, PriorDiscussion, Status, Result, CheckNote, SubmittedAt, CompetingDisplayNumbers, Submitters (names only, author first), Rank and UpvoteCount (null unless the ranking is public). Never member numbers, never upvoter identities. Response is the DTO as JSON (`application/json`); the Blazor page saves it as a file. Audit `Exported`.

- [ ] **Step 1: Tests**: `Export_requires_commission`, `Export_contains_names_but_no_member_numbers` (raw body contains the seeded name and not the member number), `Export_excludes_drafts_and_unnumbered`, `Export_rank_null_during_window`.
- [ ] **Step 2: Implement**, build, run. Expected: PASS.

### Task 29: Blazor for phase 5, docs, final changelog

**Files:**
- Modify: `AssemblyMotionDashboard` (button "An OpenSlides übertragen" opening a dialog with the checkbox "Auch eingereichte, noch nicht geprüfte Anträge übertragen" and a confirm button; afterwards a result card with created, updated, skipped and the failure list; button "JSON exportieren" that fetches the export and saves it with `JS.InvokeVoidAsync("qmDownload.blob", bytes, "antraege.json", "application/json")`, see `Pages/Forms/ExpenseReimbursement.razor.cs:368`)
- Modify: `AssemblyMotionPortalSettings` (OpenSlides meeting id field with "Verbindung prüfen")
- Modify: `AssemblyMotionCommissionDetail` (OpenSlides id, pushed revision, last error)
- Modify: `Quartermaster.Documentation/Configuration.md` (section "OpenSlides push for the motion portal": the three `openslides.*` options, the account needs the motion manage permission in the target meeting, where to find the meeting id: the number in the meeting URL)
- Modify: i18n, `V4.md`

Changelog section:

```
### Übertragung nach OpenSlides

Die Antragskommission überträgt die zugelassenen Anträge mit einem Klick in die OpenSlides-Versammlung, auf Wunsch in der Reihenfolge der Priorisierung. Spätere Korrekturen werden bei der nächsten Übertragung nachgezogen. Zusätzlich lassen sich alle Anträge als JSON-Datei exportieren.
```

- [ ] **Step 1: Implement**, build, headless screenshot of the push dialog and result card.

### Task 30: Upcoming events on the front page

**Files:**
- Modify: `Quartermaster.Blazor/Pages/Home.razor` (upcoming events card), `Quartermaster.Blazor/Pages/Home.razor.cs` only if needed
- Modify: `Quartermaster.Api/Dashboard/DashboardDTO.cs` (`DashboardEventDTO`) and `Quartermaster.Server/Dashboard/DashboardEndpoint.cs` (`HasMotionPortal`)
- Modify: i18n, `V4.md`
- Test: `Quartermaster.Server.Tests/Dashboard/DashboardUpcomingEventsTests.cs`

The front page already has an upcoming events card fed by `GET /api/dashboard` (anonymous, `FetchUpcomingEvents`, visibility filter in `GetAllowedEventVisibilities`), but its links point to `/Administration/Events/{id}`, which a normal user cannot open, so the public event page and with it the motion portal are unreachable without knowing the URL.

- The event name links to the public page `/Events/{id}` for everyone; `AllUrl` becomes `/Events`.
- Visibility stays as `GetAllowedEventVisibilities` decides: anonymous sees `Public`; signed-in users see `Public` and `MembersOnly`; users with `ViewEvents` (global or in any chapter) also see `Private`. `PublicEventDetail` must be able to show every event the card lists for that viewer; if its endpoint is stricter, align the two on this rule.
- `DashboardEventDTO.HasMotionPortal` (portal exists for the event); the row shows a button "Antragsportal" linking to `/Events/{id}/Motions` when set.
- Listed are events with a date in the future, as today (`EventRepository.GetUpcoming`), plus events without a date that have a motion portal, so a portal for the next assembly can open before its date is fixed. Undated events come after the dated ones, newest first. The visibility filter applies to both.

Changelog section:

```
### Veranstaltungen auf der Startseite

Die Startseite zeigt die kommenden Veranstaltungen jetzt mit Link zur öffentlichen Veranstaltungsseite und, falls vorhanden, direkt zum Antragsportal.
```

- [ ] **Step 1: Tests**: `Anonymous_sees_only_public_events`, `Member_sees_public_and_members_only`, `Viewer_with_permission_sees_private`, `Past_events_are_left_out`, `Undated_event_without_portal_is_left_out`, `Undated_event_with_portal_is_listed_after_dated_ones`, `Undated_private_event_with_portal_hidden_from_member`, `Has_motion_portal_is_set_for_events_with_a_portal`, and one test that the public event detail endpoint returns each listed event for the same viewer.
- [ ] **Step 2: Implement**, build, run `DashboardUpcomingEventsTests`. Expected: PASS.

### Checkpoint 5

- [ ] Build; run `HttpOpenSlidesClientTests`, `AssemblyMotionOpenSlidesPushTests`, `AssemblyMotionOpenSlidesEndpointTests`, `AssemblyMotionExportEndpointTests`, `DashboardUpcomingEventsTests`.
- [ ] Sweep the complete diff of the feature against `CLAUDE.md` (usings vs. fully qualified names, braces, access modifiers, no `@code`, no narration comments, no em or en dashes in strings and docs).
- [ ] Restart the local server (OpenSlides options stay empty on the VM). Stop and report to the user, including the steps for the first real OpenSlides push (local OpenSlides account with motion manage permission, options, meeting id).
