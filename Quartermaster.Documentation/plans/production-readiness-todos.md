# Production Readiness TODOs

## Critical (Must-Have Before Production)

### Authentication & Authorization
- [x] Bearer token auth — `Authorization: Bearer <token>` header, TokenAuthenticationHandler validates against DB, checks expiry, resolves user + claims
- [x] 25 permission identifiers defined (8 global, 17 chapter-scoped), seeded in PermissionRepository
- [x] Auth flow — login returns token + user info + permissions (global + chapter-scoped); Blazor stores in localStorage, sends via DelegatingHandler
- [x] Endpoint authorization — every admin endpoint has explicit permission check in HandleAsync; 20 endpoints remain anonymous (public APIs); list endpoints require auth-only; detail/edit/delete require specific permissions
- [x] Hierarchical permissions — view/read permissions walk ancestor chain (parent chapter grant applies to children); write permissions are exact-match only
- [x] Login UI — Login page with SSO card (disabled if SAML unconfigured) + manual login card; login/logout button in nav; redirect after login
- [x] Audit log integration — CurrentUser populated from auth claims via middleware, AuditLogRepository uses real user instead of "System"
- [x] User permission management — admin UI for granting/revoking global and chapter-scoped permissions per user
- [x] SAML SSO — complete: SamlLoginConsumeEndpoint validates response, extracts email (NameID or attribute fallback incl. OID), requires matching member, blocks exited members, creates/links user, issues token; SamlCallback Blazor page completes login; SessionEndpoint for token-based session recovery; configurable support contact for errors; email synced on member import
- [x] Template roles — full role system: `Role` entity (Identifier, Name, Description, Scope, IsSystem) + `RolePermission` (M:N) + `UserRoleAssignment` (UserId, RoleId, ChapterId). Roles are either Global or ChapterScoped, with matching permission validation. System roles are locked (can't edit/delete, permissions seeded from code). Seeded "Vorstand" (Chapter Officer) system role with the 15 default officer permissions. Officer add/delete now creates/removes role assignments instead of direct grants. Permission checks include role-derived grants transparently (via `UserGlobalPermissionRepository` + `UserChapterPermissionRepository` which inject `RoleRepository`). Admin UI: `/Administration/Roles` for CRUD, `/Administration/Roles/Assignments` for user-role-chapter assignments with user/role/chapter selectors. New `ManageRoles` global permission.
- [x] **Motion vote delegation hardening** — when `req.UserId != logged-in user`: (a) target must be a chapter officer of the motion's chapter (via User→Member→ChapterOfficer lookup), (b) caller must be an officer of the chapter or ancestor chain, OR (c) have `motions_vote_delegate` chapter permission. Self-voting unchanged.
- [x] **Add authorization to due selection list endpoint** — `GET /api/admin/dueselections` checks `ViewDueSelections` (global or chapter-scoped); chapter-scoped users see only due selections linked to their permitted chapters via MembershipApplication join
- [x] **Add authorization to member list endpoint** — `GET /api/members` checks `ViewMembers` (global or chapter-scoped); chapter-scoped users see only members in their permitted chapters
- [x] **Add authorization to membership application list endpoint** — `GET /api/admin/membershipapplications` checks `ViewApplications` (global or chapter-scoped); chapter filter intersected with auth-permitted chapters
- [x] **Add authorization to event template list endpoint** — `GET /api/eventtemplates` checks `ViewTemplates` (global or chapter-scoped); chapter-scoped users see only templates for their permitted chapters

### XSS Prevention
- [x] Add HTML sanitization to all `(MarkupString)` usage in Blazor — all 4 locations (MotionDetail, EventDetail, OptionDetail, MarkdownEditor) now use sanitized HTML via MarkdownService/TemplateRenderer
- [x] Sanitize Markdown→HTML output server-side before storing — MotionCreateEndpoint, MembershipApplicationCreateEndpoint, ChecklistItemExecutor all use `MarkdownService.ToHtml()` with Strict profile
- [x] Using `HtmlSanitizer` (Ganss.Xss) with two profiles: Strict (motions — formatting only, no clickable links/tables) and Standard (events/templates — formatting + links + tables)
- [x] Audit Fluid template rendering — TemplateRenderer now sanitizes output via MarkdownService with Standard profile

### CORS & CSRF
- [x] Removed permissive CORS policy entirely (same-origin app, not needed)
- [x] Add CSRF protection for state-changing endpoints — antiforgery middleware validates X-CSRF-TOKEN header on all POST/PUT/DELETE to /api/*, Blazor DelegatingHandler fetches and attaches tokens transparently
- [x] Set `SameSite=Strict` on antiforgery cookie; auth cookie will follow same pattern when implemented
- [x] **Add security response headers** — `SecurityHeadersMiddleware` sets `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Content-Security-Policy` (self + wasm-unsafe-eval + unsafe-inline styles), `Strict-Transport-Security` (HTTPS only, 1 year), `Referrer-Policy: strict-origin-when-cross-origin`

### Input Validation
- [x] Add FluentValidation validators for all request DTOs (18 validators using FastEndpoints `Validator<T>`, auto-discovered)
- [x] Validate page size limits (prevent requesting 100k records) — 8 `Validator<T>` classes enforce `PageSize` between 1–100 and `Page >= 1` on all paginated endpoints
- [x] Email validation: `Contains('@')` is sufficient — actual validation happens via confirmation email with click-to-verify link
- [x] Validate string lengths match database column sizes (all string fields validated against DB column limits)
- [x] Validate required fields (ChapterId, names, enum ranges, Guid.Empty checks, conditional login fields)

### Configuration & Secrets
- [x] Remove hardcoded Admin/Admin — auto-seeding is `#if DEBUG` only; production uses `dotnet run -- init-admin` CLI command with interactive prompts
- [x] Support environment variables — CLI command and default builder both support env vars; `appsettings.template.json` documents the required structure
- [x] Secrets management — only connection string needed in appsettings for production; SAML moved to Options system (DB-stored, admin UI)
- [x] SAML settings configurable — moved to Options system (`auth.saml.endpoint`, `auth.saml.client_id`, `auth.saml.certificate`), endpoints return 503 if unconfigured, `SamlSettings.cs` deleted

---

## High Priority (Should-Have Before Production)

### Audit Log
- [x] Audit log entity — AuditLog with EntityType, EntityId, Action, FieldName, OldValue, NewValue, UserId (nullable), UserDisplayName, Timestamp; per-field change tracking
- [x] Audit logging via AuditLogRepository injected into all data repositories; TODO markers for replacing "System" with authenticated user
- [x] Logged: Members (29 fields compared on update), Events (CRUD + archive + checklist ops), ChapterOfficers (add/delete), Motions (CRUD + votes + status + resolve), Options (value changes), MembershipApplications (CRUD + status), DueSelections (CRUD + status)
- [x] Member import change tracking — MemberRepository.Update now compares all fields and logs diffs via AuditLogRepository
- [x] Audit log display on Member detail page — replaces placeholder with table (Zeitpunkt, Aktion, Feld, Alter/Neuer Wert, Benutzer)
- [x] Audit log display on Event detail page — same table pattern added

### Email/Messaging System
- [x] SMTP email sending — MailKit via `EmailSendingBackgroundService`, replaces stubbed `MemberEmailService` (deleted)
- [x] SMTP configuration — 7 options in Options system (host, port, username, password, sender address/name, SSL), admin UI configurable
- [ ] Message bus abstraction — deferred; just EmailService for now, extract when second channel needed
- [x] Email queue/retry — `Channel<EmailMessage>` with `BackgroundService` consumer, 3 retries with exponential backoff
- [x] Per-member personalization — Fluid template rendering with `member.*` variables per recipient
- [x] Email sending log — `EmailLog` table with recipient, subject, status, error, attempt count, source entity traceability (EntityType+EntityId); `GET /api/emaillogs` endpoint
- [ ] Outbox pattern — partial fix applied: `HtmlBody` now persisted on `EmailLog`, and `EmailSendingBackgroundService` re-enqueues any Pending log entries on startup. This prevents email loss after a server crash/restart. **Simple fix chosen for now**: still uses in-memory `Channel<EmailMessage>` as the primary queue — if a crash happens mid-send (between channel write and SMTP completion), the email is only recovered on next restart rather than by a second worker. Revisit after production observation: may want to move to full DB-polling outbox if reliability becomes critical or if we need horizontal scaling.
- [x] **Batch SMTP sends / connection reuse** — `EmailSendingBackgroundService` now drains available messages from the channel (blocking on the first, non-blocking `TryRead` for the rest up to configured batch size), opens one SMTP connection, sends all, then disconnects. Per-message errors use existing retry logic. Connection-drop mid-batch re-queues remaining messages. Batch size configurable via `email.smtp.batch_size` option (default 50). Also optimized: SMTP config read once per batch instead of per message.

### Data Integrity
- [x] FK cascade behavior — CASCADE DELETE added for MotionVotes→Motions, ChecklistItems→Events, SystemOptions→Chapters, Tokens→Users (M002 migration)
- [x] Deletion logic — soft-delete (DeletedAt column) for Event, Motion, MembershipApplication, DueSelection, User, EventTemplate; hard-delete for Members (legal requirement) and leaf records
- [x] Unbounded member query — MemberEmailService replaced `pageSize:100000` with batched 500-per-page iteration
- [x] Database indexes — 9 secondary indexes added: Member(LastName,FirstName), Event(ChapterId), Motion(ChapterId), Chapter(ShortCode), Chapter(ExternalCode), ChecklistItem(EventId), MotionVote(MotionId), ChapterOfficer(ChapterId), Token(UserId)

### Error Handling
- [x] Global exception handler — `UseExceptionHandler` returns structured JSON for unhandled exceptions; `UseProblemDetails()` for FastEndpoints validation errors
- [x] Consistent HTTP status codes — endpoints already use 400/404 properly; 500s now return structured error JSON
- [x] Blazor ErrorBoundary — `AppErrorBoundary` wraps Router with German error message, configurable error contact from Options, recovery button
- [x] User-facing error messages — toast notifications on ALL API failures across all 24 Blazor pages; `ClientConfigService` loads error contact on startup

---

## Medium Priority (Nice-To-Have)

### Code Quality
- [x] TODOs resolved — removed stale MemberImportService audit TODO (now handled by AuditLogRepository), updated Token.cs TODO with migration note; remaining TODOs are auth-related (will be resolved with auth implementation)
- [x] Server-side wrappers removed — deleted `TemplateRenderer.cs` and `TemplateMockDataProvider.cs` from Server/Options, callers updated to use Api versions directly
- [x] TemplatePreviewEndpoint — migrated to client-side: OptionDetail.razor.cs now calls TemplateRenderer and TemplateMockDataProvider directly; server endpoint and validator removed
- [x] Null checking in endpoints — all detail endpoints already return 404 for primary entity; `?.Name ?? ""` patterns are for related entity lookups (acceptable defensive coding)
- [ ] DTO mapping standardization — deferred; current mix of Mapperly (simple) + manual (complex) is pragmatic

### Performance
- [x] N+1 query patterns — verified MotionDetailEndpoint and ChapterDetailEndpoint already batch member lookups correctly (single `WHERE IN` query)
- [x] ChapterPicker caching — static cache in Blazor, fetched once per WASM session instead of on every component init
- [x] Email recipient resolution — `GetByChapterIds()` and `GetByAdministrativeDivisionId()` replace loading all members; `FetchAllMembers()` removed
- [x] Connection pooling — `pooling=true;max pool size=20;min pool size=5` added to template and dev connection strings

### Frontend Polish
- [x] Loading indicators — already present on all 22+ pages (consistent spinner-border pattern)
- [x] Confirmation dialogs — `ConfirmDialog` component (Bootstrap modal with async ShowAsync), wired to delete checklist items, delete templates, archive/restore events
- [x] Accessibility — practical improvements pass: `aria-label` added to all 20 icon-only buttons (pagination arrows, row actions, edit/save/cancel controls, vote buttons, etc.) across pages + shared components. Labels in German to match UI language. Semantic form label associations deferred (74 unassociated `<label>` elements; works via DOM proximity for screen readers — tracked in code quality todo).
- [x] Success toasts — added to 9 pages: EventDetail (save/add/delete/archive), EventCreate, EventCreateFromTemplate, EventTemplateSave, EventTemplateList, ChapterOfficerAdd, MotionCreate, MemberImportHistory
- [x] **Option detail page improvements** — added `Description` column to OptionDefinition (M002 migration); descriptions with examples/help text on all options; FriendlyNames cleaned up (no inline examples); description shown below identifier on detail page; input fields full-width (removed max-width constraint)
- [x] **Auto-grant chapter permissions for officers** — ChapterOfficerAddEndpoint grants all `DefaultOfficerPermissions` (16 permissions) for the chapter to the officer's linked user; ChapterOfficerDeleteEndpoint revokes them. Permission list defined in `PermissionIdentifier.DefaultOfficerPermissions`.
- [x] **Admin user deletion** — `DELETE /api/users/{id}` with `users_delete` global permission; soft-deletes user, invalidates all tokens; prevents self-deletion; delete button on user detail page (permission-gated)
- [x] **Root admin gets all permissions** — SupplementDefaults grants all global permissions and all chapter-scoped permissions for the Bundesverband (root chapter) to the root admin account
- [x] **Dirty-state save buttons** — component-based `DirtyForm` system: `DirtyForm` wrapper cascades dirty state to child `FormInput`/`FormTextarea`/`FormSelect` components which call `MarkDirty()` on input; `FormSaveButton` auto-disables when form is clean (with optional `Enabled` override). Applied to OptionDetail and EventDetail.
- [x] **Nav visibility based on permissions** — Vorstandsarbeit and System dropdowns hidden when user has none of the required permissions; dividers grouped inside `RequirePermission` blocks to prevent orphans; added `AnyOfChapterPermissions` and `AnyOfPermissions` parameters to `RequirePermission` component
- [x] **User settings page** — `/Settings` page shows own account info (name, email, login method), global permissions, and chapter permissions; username in nav links to settings page; uses existing AuthService session data (no extra API call)
- [x] **Invalidate tokens on member exit** — during member import, when ExitDate is newly set and member has a linked user, all tokens for that user are deleted via `TokenRepository.DeleteAllForUser()` to force immediate logout
- [x] **Manual member CSV import via web UI** — `POST /api/members/import/upload` accepts multipart CSV, saves to temp, runs `ImportFromFile()`, returns import log; upload card added to existing MemberImportHistory page with `InputFile` component, client-side validation (.csv, 20MB limit), file size display, spinner during import
- [x] **Background service status admin page** — `/Administration/ImportStatus` shows both services: latest run stats, error details, collapsible history tables; CSV upload link; nav under System. Added `ViewAllMembers` global permission, admin div import history endpoint, `GetPermittedChapterIds` overload for split global/chapter permission checks.
- [x] **Member list orphan filter** — added `IsOrphaned` flag to `AdministrativeDivision` entity (set during import, cleared when division reappears); `OrphanedOnly` filter on member search request/repository/endpoint; checkbox toggle on member list UI; orphan warning badge + `AdminDivisionPicker` reassignment on member detail page; `PUT /api/members/{id}/admindivision` endpoint for manual reassignment
- [x] **Home page dashboard** — `GET /api/dashboard` returns permission-gated widget data with both counts and first 10 items per section. Home page renders cards with inline tables (name, chapter, submitted/created date) for: open Mitgliedsanträge, open Beitragseinstufungen, open Anträge, and upcoming Events. Each card shows total count badge + "Alle anzeigen" link; rows link directly to detail pages. Sections are hidden when the user lacks the relevant permission (null from server). Event section respects visibility (anonymous→Public only, auth→+MembersOnly, ViewEvents→+Private).
- [x] Mobile responsiveness review — audited all critical public flows at 375px (home, login, public event list/detail, membership application, motion submit, due selector) — all rendered cleanly. Main issue found: admin data tables overflowed viewport horizontally. **Fix:** wrapped all 46 admin tables across 27 files in `.table-responsive` containers — tables now scroll horizontally within their card instead of overflowing the page. Public pages need no further mobile changes; admin pages usable on tablet+ and functional on phone (tables scroll).

### Testing
- [x] Unit tests for critical business logic — 53 new tests across 7 suites: ChapterRepository (14), OptionRepository.ResolveValue (9), MotionRepository.TryAutoResolve (11), MemberImportService (7), EmailService (7), UserRepository (5); test DB fixture with auto-migration and table cleanup
- [x] **Admin division import tests** — 12 tests: initial load, file hash skip, HasCompletedInitialLoad, DB log persistence, name/postcode change detection, new division adds, postcode-based remapping, parent fallback remapping, orphan handling, member/chapter reference updates, full statistics validation. Also fixed two bugs in change detection: FK-safe parent ID remapping for new divisions, and depth-ordered deletion with child re-parenting.
- [x] **Member import tests with chapters** — 6 new tests: Bezirk-only chapter resolution, single postcode admin div match, multiple matches with city disambiguation, multiple matches without city, no postcode stays null, no matching postcode stays null. Total suite now 13 tests.
- [x] **Integration tests for API endpoints** — comprehensive per-endpoint coverage (417 integration tests, 4-6 per endpoint): happy path, 401 anonymous, 403 insufficient permission, 400 validation, 404 not-found, and behavior-specific edge cases. Organized under `Quartermaster.Server.Tests/Integration/<Feature>/` with one test class per endpoint.
- [x] **End-to-end tests for key user flows** — Playwright-based E2E tests via `TUnit.Playwright` package, headless Chromium driving a real Kestrel host. Covers login form, public pages (home, events, membership application), mobile responsiveness at 375px. Infrastructure in `Quartermaster.Server.Tests/Infrastructure/E2ETestBase.cs` + `E2ETestFactory.cs`.

---

## Low Priority (Future Enhancements)

### Features Mentioned in Specs
- [x] Public-facing event page — `/Events` (public list) and `/Events/{id}` (public detail) pages, anonymous access. Uses existing list/detail endpoints which already filter by visibility (anonymous users see Public only). Detail page renders event description as markdown (Standard sanitization profile) with `{{date}}` variable replacement. MembersOnly/Private events return "Event ist nicht öffentlich verfügbar" message. No admin UI elements shown (no checklist, no audit log, no actions).
- [x] Event status lifecycle — `EventStatus` enum (Draft/Active/Completed/Archived) replaces `IsArchived`. Auto-transitions: Draft→Active on first checklist check, Active→Completed when all items done AND event date passed (computed in `RefreshStatus` called from detail endpoint + check endpoint). Manual transitions via `PUT /api/events/{id}/status` with allowed-transition matrix (Draft↔Active, Active↔Completed, Completed↔Archived). Template creation restricted to Draft events. UI shows status badges on list/detail + context-appropriate transition buttons.
- [x] SSO/SAML member-to-user linking flow — `SsoLoginHelper.ProcessSsoLogin` auto-links on email match: finds member by SSO email → reuses/creates user → links to member → auto-grants officer permissions. Blocks exited members and deleted users. Fails cleanly with NoMember if no email match (by design — no manual linking flow needed).
- [ ] **Meeting system with agenda** — model meetings as an entity with an ordered list of agenda points. Each agenda point can optionally reference a Motion (pulled in for discussion/voting in that agenda point). Meeting lifecycle similar to events; voting on motions within a meeting context; minute-taking / protocol generation from agenda. Likely requires: Meeting entity, AgendaItem entity (with optional MotionId FK), agenda editor UI with drag-reorder, motion-picker within agenda editor, per-agenda-item notes/resolutions, protocol export (markdown/PDF).
- [x] Admin division search for email targets — added `GetDescendantIds` to `AdministrativeDivisionRepository` (BFS traversal mirroring chapter pattern) and `GetByAdministrativeDivisionIds` to `MemberRepository`; `EmailService.FetchTargetMembers` now resolves all descendant divisions so targeting a state reaches all members in cities/counties beneath it. 2 new tests verify hierarchy traversal and leaf-only targeting.
- [x] Event visibility (combined with status lifecycle work) — `EventVisibility` enum (Public/MembersOnly/Private, default Private). `GET /api/events` and `/api/events/{id}` allow anonymous access; Public events visible to everyone, MembersOnly requires auth, Private requires ViewEvents permission. Visibility filter on list endpoint based on user auth state + permissions. Visibility selector on event detail form. **Note:** Public-facing event page itself is a separate future task — backend is ready.
- [x] **Login brute-force protection** — `LoginAttempts` table logs every attempt (success/fail) with IP+username. Lockout triggers when `max_attempts` failures occur for a (IP, user) pair within `duration_minutes`. Both configurable via options system (`auth.lockout.max_attempts`, `auth.lockout.duration_minutes`). Locked requests return 429 without revealing account state. Admin page at `/Administration/LoginLockouts` lists all active lockouts with unlock button; nav link under System > Benutzer.
