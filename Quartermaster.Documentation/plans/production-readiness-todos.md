# Production Readiness TODOs

## Critical (Must-Have Before Production)

### Authentication & Authorization
- [ ] Replace ALL `AllowAnonymous()` endpoints with proper authentication (53+ endpoints)
- [ ] Use the EXISTING Token infrastructure (`Token` entity, `TokenRepository`) — NOT JWT. Tokens are stored server-side so permission changes take effect immediately without waiting for token expiry
- [ ] Auth flow: login → server creates Token → client stores token (cookie or header) → each request validated against DB
- [ ] Token validation as a FastEndpoints preprocessor: look up token in DB, check expiry, resolve User + permissions
- [ ] Wire up the existing `ChapterPermissionRequirement` preprocessor to relevant endpoints
- [ ] Chapter-scoped permissions are HIERARCHICAL: a parent chapter's officers can administrate all child chapters (e.g., Bundesverband officer can manage NDS, NDS officer can manage NDS sub-chapters). Permission check must walk up the chapter ancestor chain.
- [ ] Define permission matrix: which roles can access which endpoints
- [ ] Endpoints that SHOULD remain anonymous: public motion submission, membership application, due selection, public motion listing
- [ ] Implement login UI (currently no frontend login page exists)
- [ ] SAML SSO integration for member-to-user linking (flow exists partially in `SamlLoginStartEndpoint`/`SamlLoginConsumeEndpoint`)

### XSS Prevention
- [x] Add HTML sanitization to all `(MarkupString)` usage in Blazor — all 4 locations (MotionDetail, EventDetail, OptionDetail, MarkdownEditor) now use sanitized HTML via MarkdownService/TemplateRenderer
- [x] Sanitize Markdown→HTML output server-side before storing — MotionCreateEndpoint, MembershipApplicationCreateEndpoint, ChecklistItemExecutor all use `MarkdownService.ToHtml()` with Strict profile
- [x] Using `HtmlSanitizer` (Ganss.Xss) with two profiles: Strict (motions — formatting only, no clickable links/tables) and Standard (events/templates — formatting + links + tables)
- [x] Audit Fluid template rendering — TemplateRenderer now sanitizes output via MarkdownService with Standard profile

### CORS & CSRF
- [x] Removed permissive CORS policy entirely (same-origin app, not needed)
- [x] Add CSRF protection for state-changing endpoints — antiforgery middleware validates X-CSRF-TOKEN header on all POST/PUT/DELETE to /api/*, Blazor DelegatingHandler fetches and attaches tokens transparently
- [x] Set `SameSite=Strict` on antiforgery cookie; auth cookie will follow same pattern when implemented

### Input Validation
- [x] Add FluentValidation validators for all request DTOs (18 validators using FastEndpoints `Validator<T>`, auto-discovered)
- [ ] Validate page size limits (prevent requesting 100k records)
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
- [ ] Outbox pattern — deferred; in-memory Channel sufficient for now

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
- [ ] TemplatePreviewEndpoint — still needed (OptionDetail.razor.cs calls it server-side); TODO added to migrate to client-side
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
- [ ] Accessibility — ARIA labels on interactive components (deferred)
- [x] Success toasts — added to 9 pages: EventDetail (save/add/delete/archive), EventCreate, EventCreateFromTemplate, EventTemplateSave, EventTemplateList, ChapterOfficerAdd, MotionCreate, MemberImportHistory
- [ ] Mobile responsiveness review (deferred)

### Testing
- [x] Unit tests for critical business logic — 53 new tests across 7 suites: ChapterRepository (14), OptionRepository.ResolveValue (9), MotionRepository.TryAutoResolve (11), MemberImportService (7), EmailService (7), UserRepository (5); test DB fixture with auto-migration and table cleanup
- [ ] Integration tests for API endpoints (deferred)
- [ ] End-to-end tests for key user flows (deferred)

---

## Low Priority (Future Enhancements)

### Features Mentioned in Specs
- [ ] Public-facing event page (mentioned as future scope in events design)
- [ ] Event status lifecycle (Draft → Active → Completed → Archived)
- [ ] SSO/SAML member-to-user linking flow
- [ ] More checklist item types (extensible enum designed for this)
- [ ] Admin division search for email targets (AdminDivisionPicker created but backend needs proper member-by-division query)
