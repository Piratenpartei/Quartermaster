# Testing Improvements Plan

**Goal:** Close all testing TODOs from `production-readiness-todos.md` and `code-quality-todos.md`:
- Integration tests for API endpoints (comprehensive, 4–6 per endpoint)
- End-to-end tests for key user flows (Playwright via TUnit compat)
- Edge-case coverage review of existing unit-test suites

**Philosophy for this round:** *Too many tests beats too few.* We accept longer CI runs in exchange for broad coverage.

**Scale estimate:** ~77 endpoints × 4–6 tests ≈ **300–450 integration tests**, plus ~7 E2E flows, plus ~50–100 new unit tests from the coverage review. Current suite: 283 tests → target ≈ **700–900 tests**.

---

## Test Taxonomy

| Layer | Scope | Tech | Speed | Where |
|---|---|---|---|---|
| **Unit** | Pure logic, validators, single repository against DB | TUnit + LinqToDB on real MySQL | fast | `Quartermaster.Server.Tests/<Area>/` |
| **Integration** | One HTTP request through the full pipeline (auth + validation + endpoint + DB) against an in-process `WebApplicationFactory` | TUnit + WebApplicationFactory + MySQL | medium | `Quartermaster.Server.Tests/Integration/<Area>/` |
| **E2E** | Browser driving the Blazor UI against a live in-process host | TUnit.Playwright + Chromium | slow | `Quartermaster.Server.Tests/E2E/<Flow>/` |

Organize tests by **layer first, feature area second** so you can run any subset (`--treenode-filter "/*/*/Integration/*"`).

---

## Decisions (confirmed with user)

- Integration tests: **comprehensive per endpoint** — happy path + auth denial + permission denial + validation errors + edge cases
- E2E: **Playwright via TUnit compat package**, **Chromium only**, **in-process host** (`WebApplicationFactory`)
- Parallelism: **per-worker databases** (`quartermaster_test_w1`, `_w2`, …) so TUnit can run tests in parallel
- Project layout: **single test project** (`Quartermaster.Server.Tests`), well-structured sub-namespaces
- E2E flow set (expandable): login, membership application → approval, event lifecycle, motion vote, member CSV import, chapter officer add/remove, due selector public flow

---

# Phase 1 — Test Infrastructure

The current `TestDatabaseFixture` is a static singleton with a single shared database and `CleanAllTables()` between tests (forcing `[NotInParallel]`). Integration/E2E multiply test count significantly — we need true parallelism.

## Task 1.1 — Per-worker database isolation

Replace the static fixture with a **worker-scoped** one. TUnit assigns each test to a worker thread; hash `Environment.CurrentManagedThreadId` or use `TUnit`'s worker ID to pick a DB name per worker.

**Approach:**
- `TestDatabaseFixture` becomes instance-based, keyed by worker ID
- On first use per worker: create `quartermaster_test_w{id}`, run migrations once
- `CleanAllTables()` per-test — now safe to run in parallel because each worker owns its own DB
- All existing `[NotInParallel]` attributes removed
- Add a teardown hook (global assembly-level) that optionally drops worker DBs after the run (behind a flag, so devs can inspect state)

**Files:**
- `Quartermaster.Server.Tests/Infrastructure/TestDatabaseFixture.cs` — rewrite
- `Quartermaster.Server.Tests/Infrastructure/WorkerId.cs` — new, resolves stable worker IDs
- `Quartermaster.Server.Tests/Infrastructure/GlobalTestHooks.cs` — new, assembly-level setup/teardown

**Tasks:**
- [x] Add `WorkerId` helper that returns a stable `int` per TUnit worker
- [x] Rewrite `TestDatabaseFixture` as per-worker (connection string derived from worker ID)
- [x] Add `[Before(Assembly)]` hook that probes connection, fails fast with friendly error if MySQL unavailable
- [x] Add optional `[After(Assembly)]` hook to drop worker DBs (controlled by `QM_TEST_KEEP_DB=1` env var)
- [x] Remove all `[NotInParallel]` from existing test classes
- [x] Verify existing 283 tests still pass in parallel mode

## Task 1.2 — Test data builders

Current tests hand-construct entities with `new Chapter { ... }`. For integration tests where we need realistic domain graphs (user → token → role → chapter permission), builders keep tests terse.

**Approach:**
- `TestDataBuilder` facade with chainable methods: `SeedChapter()`, `SeedUser()`, `SeedUserWithPermissions()`, `SeedMember()`, `SeedEvent()`, etc.
- Builders return the inserted entity (so tests can use its ID)
- Each builder takes optional overrides; sane defaults for everything else
- Null Island admin division seeded by default (FK requirement)

**Files:**
- `Quartermaster.Server.Tests/Infrastructure/TestDataBuilder.cs` — new
- `Quartermaster.Server.Tests/Infrastructure/Builders/*Builder.cs` — one per entity family

**Tasks:**
- [x] Create `TestDataBuilder` with `DbContext` field
- [x] Add builders for: `Chapter` (with parent chain), `User` (with token), `Member`, `AdministrativeDivision`, `Event`, `Motion`, `Role`, `MembershipApplication`, `DueSelection`
- [x] Helper `SeedAuthenticatedUser(string[] globalPerms, Dictionary<Guid, string[]> chapterPerms)` returns `(User, string token)`
- [x] Replace existing hand-rolled setups in 3–4 existing test classes as a sanity check

## Task 1.3 — Integration test host (`WebApplicationFactory<Program>`)

FastEndpoints + our `Program.cs` should work with `WebApplicationFactory<Program>` directly. We override:
- connection string → per-worker test DB
- background services → disabled (we test them separately)
- SMTP → in-memory capture
- antiforgery → disabled per-request via test header (or real flow where we test CSRF explicitly)

**Approach:**
- `IntegrationTestFactory : WebApplicationFactory<Program>` per worker
- Override `ConfigureWebHost` to swap services
- Disable hosted services (`AdminDivisionImportHostedService`, `MemberImportHostedService`, `EmailSendingBackgroundService`) via service descriptor removal
- Replace `EmailService` (or the SMTP sender) with a test double that appends to a list
- Expose `HttpClient` helper methods: `AuthenticatedClient(user)`, `AnonymousClient()`

**Files:**
- `Quartermaster.Server.Tests/Infrastructure/IntegrationTestFactory.cs` — new
- `Quartermaster.Server.Tests/Infrastructure/TestEmailSink.cs` — new
- `Quartermaster.Server.Tests/Infrastructure/IntegrationTestBase.cs` — new (base class with factory, builder, cleanup)
- `Quartermaster.Server/Program.cs` — add `public partial class Program { }` sentinel if needed so `WebApplicationFactory<Program>` resolves

**Tasks:**
- [x] Make `Program` class accessible to test project (partial class marker, or `InternalsVisibleTo`)
- [x] Implement `IntegrationTestFactory` with background service removal
- [x] Implement `TestEmailSink` capturing `EmailMessage` to a thread-safe list
- [x] Implement `IntegrationTestBase` with `Factory`, `Builder`, `CleanupAsync()`
- [x] Add helper `PostJsonAsync<TReq>(string url, TReq req, string? token)` with CSRF handling
- [x] Smoke test: one integration test hitting `GET /api/chapters` returns 200

## Task 1.4 — Playwright / E2E host

E2E tests need the Blazor WASM app served alongside the API. `WebApplicationFactory` serves static files from the Blazor project's `wwwroot` after publish. Simpler path: have the test fixture do a `dotnet publish Quartermaster.Blazor` once per run, then `UseWebRoot(<published wwwroot>)` in the factory.

**Approach:**
- Install `TUnit.Playwright` package
- Install Chromium via `pwsh -File bin/Debug/net10.0/playwright.ps1 install chromium` (one-time CI setup)
- `E2ETestFactory : IntegrationTestFactory` adds static-file serving and Blazor WASM MIME types
- `E2ETestBase` extends TUnit.Playwright's `PageTest` equivalent; sets `BaseURL` to the Kestrel address from the test host
- Pre-publish step: MSBuild target that runs `dotnet publish Quartermaster.Blazor -c Debug -o bin/TestWebRoot` before tests

**Files:**
- `Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj` — add `TUnit.Playwright` package, MSBuild target
- `Quartermaster.Server.Tests/Infrastructure/E2ETestFactory.cs` — new
- `Quartermaster.Server.Tests/Infrastructure/E2ETestBase.cs` — new
- `Quartermaster.Server.Tests/Infrastructure/PlaywrightInstall.cs` — new, one-time install in assembly setup

**Tasks:**
- [x] Add `TUnit.Playwright` package reference
- [x] Add MSBuild `BeforeTargets="Build"` target that publishes Blazor to `bin/$(Configuration)/net10.0/TestWebRoot`
- [x] Implement `E2ETestFactory` wiring static files + WASM content types
- [x] Install Chromium via assembly-level hook (first run downloads, subsequent runs skip)
- [x] Implement `E2ETestBase` with `Page`, `BaseURL`, per-test browser context, screenshot-on-failure
- [x] Smoke test: navigate to `/Login`, assert page title

---

# Phase 2 — Unit-Test Coverage Review

Audit existing suites for edge cases before adding integration tests. This is cheap, targeted, and the gaps here will otherwise show up as brittle integration tests.

**For each existing suite, walk through:** empty/null inputs, boundary values (0, 1, max), unicode/German special chars (ä ö ü ß, emoji, RTL), timezone edges (DST transitions, UTC vs local), concurrent modifications, FK cascade behaviors, deeply-nested chapter trees (10+ levels), malformed inputs, duplicates, expired tokens, race conditions.

## Task 2.1 — Per-suite audit

For each existing suite, create a TODO list of missing cases, then add tests.

**Suites to audit:**
- [x] `Chapters/ChapterRepositoryTests` — deep hierarchies, circular-parent rejection, orphan handling, search with unicode
- [x] `Options/OptionRepositoryTests` — chapter override precedence, inheritance collapse, default-value fallback with missing parent
- [x] `Motions/MotionRepositoryTests` — auto-resolve boundary conditions (exactly at threshold, tied votes, all-abstain)
- [x] `Members/MemberImportServiceTests` — malformed CSV rows, duplicate member numbers, BOM handling, trailing whitespace, empty fields, field count mismatch
- [x] `AdministrativeDivisions/AdminDivisionImportServiceTests` — depth-10 hierarchies, cycle in imported data, all-removed edge case
- [x] `Email/EmailServiceTests` — template rendering with missing variables, empty recipient list, batch size boundary
- [x] `Users/UserRepositoryTests` — case-insensitive email match, unicode usernames, deleted-user lookup
- [x] `Roles/RoleRepositoryTests` — system role edit rejection, assignment for Global role with non-null ChapterId (invalid combo)
- [x] All validator suites — Guid.Empty, string at exact column length + 1, whitespace-only strings, control chars

**Deliverable:** per suite, +5–10 edge-case tests. Target: **~80 new unit tests** total.

## Task 2.2 — Cross-cutting concern tests (new suites)

Areas with no existing coverage that aren't endpoint-shaped:

- [x] `Authentication/TokenAuthenticationHandlerTests` — expired token, missing token, malformed header, revoked token, soft-deleted user, token for non-existent user
- [x] `Authentication/EndpointAuthorizationHelperTests` — global perm grants, chapter perm with inheritance (up ancestor chain), perm via role assignment, perm via officer role
- [x] `Permissions/PermissionInheritanceTests` — 10-level chapter tree, grant at root resolves for leaf, grant at leaf does NOT resolve for root, write-perm exact-match enforcement
- [x] `LoginAttempts/LockoutLogicTests` — exactly-at-threshold, sliding window boundary, lockout expiry, lockout per-(IP+user) isolation
- [x] `Antiforgery/AntiforgeryIntegrationTests` — token issuance, rejection without header, rejection with wrong token, GET skips check
- [x] `SecurityHeaders/SecurityHeadersMiddlewareTests` — all headers present, HSTS only on HTTPS, CSP wasm-unsafe-eval present

---

# Phase 3 — Integration Tests (per-endpoint)

**Convention:** one test class per endpoint, named `{EndpointName}IntegrationTests`. Tests inherit `IntegrationTestBase`. Use `TestDataBuilder` for setup. Use `_factory.AuthenticatedClient(user, token)` or `.AnonymousClient()`.

## Task 3.1 — Endpoint test templates

Define canonical test patterns by endpoint shape. Every endpoint gets tested against its applicable template.

### Template: List endpoint (paginated)
- [x] `Returns_401_when_anonymous` (if auth required)
- [x] `Returns_403_when_user_lacks_permission`
- [x] `Returns_empty_page_when_no_data`
- [x] `Returns_single_page_of_data`
- [x] `Paginates_correctly_across_multiple_pages`
- [x] `Rejects_page_size_over_100` (validator)
- [x] `Rejects_negative_page` (validator)
- [x] `Respects_chapter_scoping` (chapter-scoped users see only their chapters)
- [x] `Search_filter_matches_case_insensitively` (if searchable)

### Template: Detail endpoint
- [x] `Returns_401_when_anonymous`
- [x] `Returns_403_when_user_lacks_permission`
- [x] `Returns_404_for_nonexistent_id`
- [x] `Returns_404_for_soft_deleted_entity`
- [x] `Returns_entity_with_all_fields_populated`
- [x] `Chapter_scoped_user_sees_only_their_chapter_entities`

### Template: Create endpoint
- [x] `Returns_401_when_anonymous`
- [x] `Returns_403_when_user_lacks_permission`
- [x] `Returns_400_for_missing_required_field`
- [x] `Returns_400_for_string_over_column_length`
- [x] `Returns_400_for_Guid_Empty_refs`
- [x] `Returns_400_for_nonexistent_FK_targets` (or 404 depending on convention)
- [x] `Creates_entity_and_returns_201`
- [x] `Writes_audit_log_entry`
- [x] `CSRF_token_required_for_state_change`

### Template: Update endpoint
- [x] `Returns_401_when_anonymous`
- [x] `Returns_403_when_user_lacks_permission`
- [x] `Returns_404_for_nonexistent_id`
- [x] `Returns_400_for_invalid_fields`
- [x] `Updates_all_mutable_fields`
- [x] `Writes_per_field_audit_diff`
- [x] `Does_not_allow_changing_immutable_fields` (e.g., Id, CreatedAt)

### Template: Delete endpoint
- [x] `Returns_401_when_anonymous`
- [x] `Returns_403_when_user_lacks_permission`
- [x] `Returns_404_for_nonexistent_id`
- [x] `Soft_deletes_entity_and_returns_204` (or hard-deletes for leaf entities)
- [x] `Soft_deleted_entity_no_longer_appears_in_list`
- [x] `Cascades_to_dependent_rows_correctly`
- [x] `Writes_audit_log`

## Task 3.2 — Endpoint-by-endpoint implementation

For each of the 77 endpoints, apply the matching template. Additional per-endpoint cases below call out non-obvious behavior.

### Admin (due selections, membership applications)
- [x] `DueSelectionListEndpoint` — chapter scoping intersection logic, pending-only filter
- [x] `DueSelectionDetailEndpoint` — chapter scoping, related member lookup
- [x] `DueSelectionProcessEndpoint` — status transition matrix, email fanout triggers
- [x] `MembershipApplicationListEndpoint` — chapter filter intersected with auth-permitted chapters, status filter
- [x] `MembershipApplicationDetailEndpoint`
- [x] `MembershipApplicationProcessEndpoint` — approve transitions to member-linked state, reject sets reason

### AdministrativeDivisions
- [x] `AdminDivisionImportHistoryEndpoint` — paginated, newest-first
- [x] `AdministrativeDivisionChildrenEndpoint` — depth traversal
- [x] `AdministrativeDivisionRootsEndpoint` — returns only root-level divisions
- [x] `AdministrativeDivisionSearchEndpoint` — name + postcode match, limit enforcement

### Antiforgery
- [x] `AntiforgeryTokenEndpoint` — issues token, sets cookie, subsequent POST with matching header succeeds, missing header → 400

### AuditLog
- [x] `AuditLogEndpoint` — filter by entity type + id, permission-gated, chronological order

### ChapterAssociates
- [x] `ChapterOfficerAddEndpoint` — auto-grants 16 default permissions, creates Vorstand role assignment, blocks duplicates
- [x] `ChapterOfficerDeleteEndpoint` — revokes all default permissions, removes role assignment
- [x] `ChapterOfficerListEndpoint` — scoped to chapter

### Chapters
- [x] `ChapterListEndpoint`, `ChapterDetailEndpoint`, `ChapterSearchEndpoint`, `ChapterRootsEndpoint`, `ChapterChildrenEndpoint`
- [x] `ChapterForDivisionEndpoint` — admin-div → chapter resolution, postcode fallback

### Config
- [x] `ClientConfigEndpoint` — anonymous accessible, returns error contact + SAML availability

### Dashboard
- [x] `DashboardEndpoint` — each section nulls out when user lacks permission, counts + first-10-items shape, anonymous sees Public events only, auth sees MembersOnly, ViewEvents sees all

### DueSelector (public)
- [x] `DueSelectionCreateEndpoint` — anonymous allowed, validates member match, idempotency per member?

### Email
- [x] `EmailLogEndpoint` — permission-gated, paginated, filter by entity

### Events
- [x] `EventListEndpoint` — visibility filtering (anonymous → Public only, auth → MembersOnly, ViewEvents → Private), chapter scoping, status filter
- [x] `EventDetailEndpoint` — visibility respected, checklist populated, audit log populated
- [x] `EventCreateEndpoint` — defaults to Draft + Private, chapter-scoped perm check
- [x] `EventUpdateEndpoint` — status-gated edits, visibility change updates ACL
- [x] `EventArchiveEndpoint` — status transition matrix (Completed → Archived only)
- [x] `EventStatusUpdateEndpoint` — allowed-transition matrix, auto-transitions via `RefreshStatus`
- [x] `EventFromTemplateEndpoint` — copies checklist, inherits chapter
- [x] `EventTemplateCreateEndpoint` — only from Draft event
- [x] `EventTemplateListEndpoint`, `EventTemplateDetailEndpoint`, `EventTemplateDeleteEndpoint`
- [x] `ChecklistItemAddEndpoint`, `UpdateEndpoint`, `DeleteEndpoint`, `ReorderEndpoint`
- [x] `ChecklistItemCheckEndpoint` — Draft→Active transition on first check, fires email via ChecklistItemExecutor
- [x] `ChecklistItemUncheckEndpoint` — idempotent, reverses status if needed

### MembershipApplications (public)
- [x] `MembershipApplicationCreateEndpoint` — anonymous allowed, sanitizes markdown, honors chapter

### Members
- [x] `MemberListEndpoint` — chapter scoping, orphan filter, search, pagination
- [x] `MemberDetailEndpoint` — chapter scoping, audit log populated
- [x] `MemberAdminDivisionUpdateEndpoint` — orphan-flag recompute, audit entry
- [x] `MemberImportHistoryEndpoint`, `MemberImportTriggerEndpoint`, `MemberImportUploadEndpoint` — multipart file, 20MB limit, invalid CSV → 400

### Motions
- [x] `MotionListEndpoint` — chapter scoping, status filter
- [x] `MotionDetailEndpoint` — vote counts, per-member vote resolution
- [x] `MotionCreateEndpoint` — markdown sanitization, defaults to Open
- [x] `MotionStatusEndpoint` — manual resolve transitions
- [x] `MotionVoteEndpoint` — self-vote happy path, delegation auth (target officer check, caller officer OR delegate perm), auto-resolve trigger

### Options
- [x] `OptionListEndpoint` — global vs chapter-scoped return
- [x] `OptionUpdateEndpoint` — value validation per OptionDefinition.Kind

### Permissions
- [x] `PermissionListEndpoint` — returns all 25 seeded permissions

### Roles
- [x] `RoleListEndpoint` — system + custom roles
- [x] `RoleCreateEndpoint` — permission scope validation (Global role cannot have chapter permissions)
- [x] `RoleUpdateEndpoint` — cannot edit system roles
- [x] `RoleDeleteEndpoint` — cannot delete system roles, cascades assignments
- [x] `RoleAssignmentListEndpoint`, `CreateEndpoint`, `DeleteEndpoint` — Global role requires null ChapterId, ChapterScoped requires non-null

### Users
- [x] `LoginEndpoint` — manual login happy path, wrong password → 401, locked-out IP+user → 429, non-existent user → 401 (no account disclosure), exited member → 401
- [x] `LoginLockoutListEndpoint`, `UnlockEndpoint` — permission-gated, unlock clears attempts
- [x] `SamlLoginStartEndpoint`, `SamlLoginConsumeEndpoint` — 503 when unconfigured, success flow with email match, exited-member rejection
- [x] `OidcLoginStartEndpoint`, `OidcCallbackEndpoint` — parallel to SAML
- [x] `SessionEndpoint` — token-based recovery, returns user+perms
- [x] `UserListEndpoint` — permission-gated
- [x] `UserDetailEndpoint` — returns perms breakdown
- [x] `UserDeleteEndpoint` — prevents self-deletion, invalidates tokens, soft-deletes
- [x] `UserSettingsEndpoint` — returns own settings, permission-gated for others

### TestData (dev-only)
- [x] `TestDataSeedEndpoint` — if present in test env, happy path only

---

# Phase 4 — End-to-End Tests (Playwright)

**Convention:** one test class per flow, named `{Flow}E2ETests`. Tests inherit `E2ETestBase`. Each test is self-contained (seeds its own data via `TestDataBuilder` before launching browser). Screenshots captured on failure automatically.

## Task 4.1 — E2E flow tests

### Login Flow (`LoginFlowE2ETests`)
- [x] `Manual_login_succeeds_with_valid_credentials` — nav shows username, dashboard visible
- [x] `Manual_login_shows_error_on_wrong_password`
- [x] `Manual_login_locks_out_after_n_attempts`
- [x] `Logout_clears_session_and_redirects_to_login`
- [x] `SSO_card_disabled_when_saml_unconfigured`
- [x] `Session_recovery_from_localStorage_token_on_reload`

### Membership Application Flow (`MembershipApplicationE2ETests`)
- [x] `Anonymous_user_can_submit_application` — fill form, submit, success toast
- [x] `Admin_sees_new_application_in_list`
- [x] `Admin_approves_application_creates_member`
- [x] `Admin_rejects_application_with_reason`
- [x] `Validation_errors_shown_inline`

### Event Lifecycle Flow (`EventLifecycleE2ETests`)
- [x] `Officer_creates_draft_event`
- [x] `Officer_adds_checklist_items`
- [x] `Checking_first_item_transitions_to_Active`
- [x] `Completing_all_items_after_event_date_transitions_to_Completed`
- [x] `Officer_archives_completed_event`
- [x] `Officer_creates_template_from_draft_event`
- [x] `Officer_creates_event_from_template` — checklist copied

### Motion Voting Flow (`MotionVotingE2ETests`)
- [x] `Officer_submits_motion_with_markdown_description`
- [x] `Officer_votes_Ja_on_motion` — vote count updates
- [x] `Second_officer_votes_Nein` — count updates
- [x] `Meeting_threshold_triggers_auto_resolve`
- [x] `Delegation_flow_vote_cast_for_other_officer`

### Member CSV Import Flow (`MemberImportE2ETests`)
- [x] `Admin_uploads_valid_CSV_and_sees_import_log`
- [x] `Invalid_CSV_shows_error_without_partial_import`
- [x] `20MB_limit_enforced_client_side`
- [x] `Import_history_shows_previous_runs`

### Chapter Officer Permission Grant Flow (`ChapterOfficerPermissionsE2ETests`)
- [x] `Adding_officer_grants_default_permissions` — verify by logging in as officer, checking nav visibility
- [x] `Removing_officer_revokes_permissions`
- [x] `Role_assignment_created_automatically`

### Due Selector Public Flow (`DueSelectorE2ETests`)
- [x] `Anonymous_member_selects_due_tier`
- [x] `Admin_sees_submission_in_list`
- [x] `Admin_processes_due_selection`

### Smoke pack (`SmokeE2ETests`)
- [x] `Home_page_loads_for_anonymous_user`
- [x] `Public_event_list_renders`
- [x] `Nav_menu_collapses_at_375px`
- [x] `All_admin_tables_scroll_horizontally_at_375px`

---

# Phase 5 — CI Integration & Cleanup

## Task 5.1 — CI pipeline
- [x] Document local test DB requirements (MySQL running on localhost, root no password)
- [x] Add `dotnet test` invocation to any CI scripts that exist; ensure Playwright install runs once
- [x] Capture test timing; flag slow tests (> 5s) for review
- [x] Artifact upload: Playwright traces + screenshots on failure

## Task 5.2 — Mark TODOs complete
- [x] `production-readiness-todos.md` — check off "Integration tests for API endpoints" and "End-to-end tests for key user flows"
- [x] `code-quality-todos.md` — check off "Test coverage review"

---

# Implementation Order

Phases must be done sequentially (each builds on the previous), but tasks within a phase can be parallelized.

1. **Phase 1** (infrastructure) — blocks everything. Do 1.1 → 1.2 → 1.3 → 1.4 in order.
2. **Phase 2** (coverage review) — can start in parallel with Phase 3 once Phase 1.1–1.2 are done.
3. **Phase 3** (integration tests) — biggest chunk. Parallelize by endpoint area: do Chapters + Users + Auth first (everything else depends on them for setup), then fan out.
4. **Phase 4** (E2E) — after Phase 1.4 and enough of Phase 3 that backend bugs are caught at the integration layer (cheap) rather than E2E (expensive).
5. **Phase 5** (CI + cleanup) — last.

# Open Questions / Risks

- **MySQL load:** per-worker DBs means N × migration runs at startup. If TUnit uses 8 workers, that's 8 DBs × ~25 migrations ≈ 200 DDL statements on cold start. Should be fast on localhost but worth measuring.
- **Blazor WASM hosting in `WebApplicationFactory`:** untested combo — may require MIME-type tweaks (`.wasm`, `.dat`, `.dll`, `.blat`). Fallback: launch real server via `dotnet run` if in-process proves flaky.
- **Playwright flakiness:** WASM boot time is non-trivial; need generous default timeouts and explicit `WaitForResponseAsync` on API calls rather than sleep-based waits.
- **Test data isolation drift:** with comprehensive coverage, the probability of one test leaking state into another grows. `CleanAllTables()` per test is mandatory; consider also asserting table-row-counts are zero at test start as a tripwire.
