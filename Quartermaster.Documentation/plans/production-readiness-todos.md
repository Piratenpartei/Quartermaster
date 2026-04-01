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
- [ ] Add HTML sanitization to all `(MarkupString)` usage in Blazor (3+ locations: MotionDetail, EventDetail, OptionDetail)
- [ ] Sanitize Markdown→HTML output server-side before storing (MotionCreateEndpoint, MembershipApplicationCreateEndpoint, ChecklistItemExecutor)
- [ ] Consider using a library like `HtmlSanitizer` NuGet package
- [ ] Audit all Fluid template rendering for injection risks

### CORS & CSRF
- [ ] Replace `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` with specific allowed origins
- [ ] Add CSRF protection for state-changing endpoints (POST/PUT/DELETE)
- [ ] Set `SameSite` cookie attribute when session-based auth is implemented

### Input Validation
- [x] Add FluentValidation validators for all request DTOs (18 validators using FastEndpoints `Validator<T>`, auto-discovered)
- [ ] Validate page size limits (prevent requesting 100k records)
- [x] Email validation: `Contains('@')` is sufficient — actual validation happens via confirmation email with click-to-verify link
- [x] Validate string lengths match database column sizes (all string fields validated against DB column limits)
- [x] Validate required fields (ChapterId, names, enum ranges, Guid.Empty checks, conditional login fields)

### Configuration & Secrets
- [ ] Remove hardcoded `Admin/Admin` default credentials from appsettings
- [ ] Support environment variables for database connection string
- [ ] Support secrets management for production deployment
- [ ] Make SAML settings configurable per environment

---

## High Priority (Should-Have Before Production)

### Audit Log
- [ ] Design audit log entity (Who, What, When, EntityType, EntityId, OldValue, NewValue)
- [ ] Implement audit logging middleware or repository wrapper
- [ ] Log changes to: Members, Events, ChapterOfficers, Motions, Options, MembershipApplications
- [ ] Member import already has a TODO stub for change tracking (`MemberImportService.cs:77`)
- [ ] Add audit log display on Member detail page (placeholder already exists)
- [ ] Add audit log display on Event detail page

### Email/Messaging System
- [ ] Implement actual SMTP email sending (currently stubbed in `MemberEmailService`)
- [ ] Add SMTP configuration options (server, port, credentials, sender address)
- [ ] Design message bus abstraction for extensibility (email now, Slack/Matrix/webhook later)
- [ ] Add email queue/retry mechanism (don't block request on SMTP)
- [ ] Email template rendering with per-member personalization using Fluid
- [ ] Email sending log (what was sent, to whom, when, success/failure)
- [ ] Consider: outbox pattern for reliable message delivery

### Data Integrity
- [ ] Review foreign key cascade behavior (currently no CASCADE DELETE configured)
- [ ] Add proper deletion logic: soft-delete vs cascade for each entity relationship
- [ ] Fix unbounded member query in MemberEmailService (hardcoded `pageSize: 100000`)
- [ ] Add database indexes for common query patterns (member search by name, events by chapter)

### Error Handling
- [ ] Add global exception handling middleware (FastEndpoints supports this)
- [ ] Return proper HTTP status codes consistently (400 for validation, 404 for not found, 500 for server errors)
- [ ] Add Blazor ErrorBoundary component to catch unhandled frontend errors
- [ ] Add user-facing error messages for API failures in Blazor pages

---

## Medium Priority (Nice-To-Have)

### Code Quality
- [ ] Resolve all TODO/FIXME comments in codebase (7 found)
- [ ] Move the existing server-side `TemplateRenderer`/`TemplateMockDataProvider` wrappers to use the shared Api versions directly (spec TODO from events design)
- [ ] Remove the server-side `TemplatePreviewEndpoint` once OptionDetail preview is migrated to client-side
- [ ] Add consistent null checking in endpoints (several return `chapter?.Name ?? ""` without 404)
- [ ] Standardize DTO mapping (some use Mapperly, some manual mapping — pick one pattern)

### Performance
- [ ] Fix N+1 query patterns in MotionDetailEndpoint and ChapterDetailEndpoint
- [ ] Add caching for chapter list (loaded on every page via ChapterPicker)
- [ ] Optimize member email recipient resolution (currently loads all members)
- [ ] Consider database connection pooling configuration

### Frontend Polish
- [ ] Add loading indicators on all async operations
- [ ] Add confirmation dialogs for destructive actions (delete checklist items, archive events)
- [ ] Improve accessibility (ARIA labels on interactive components)
- [ ] Add toast notifications for save/delete operations (ToastService exists but isn't used everywhere)
- [ ] Mobile responsiveness review

### Testing
- [ ] Add unit tests for critical business logic (motion auto-resolution, member import, chapter resolution)
- [ ] Add integration tests for API endpoints
- [ ] Add end-to-end tests for key user flows (membership application, event creation from template)

---

## Low Priority (Future Enhancements)

### Features Mentioned in Specs
- [ ] Public-facing event page (mentioned as future scope in events design)
- [ ] Event status lifecycle (Draft → Active → Completed → Archived)
- [ ] SSO/SAML member-to-user linking flow
- [ ] More checklist item types (extensible enum designed for this)
- [ ] Admin division search for email targets (AdminDivisionPicker created but backend needs proper member-by-division query)
