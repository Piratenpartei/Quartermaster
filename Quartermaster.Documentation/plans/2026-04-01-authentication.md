# Authentication & Authorization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full authentication (Bearer token) and granular authorization (per-entity CRUD permissions, chapter-scoped with hierarchical read inheritance) to all endpoints, with a Blazor login UI featuring SSO and manual login options.

**Architecture:** Login returns a Bearer token. A FastEndpoints preprocessor validates the token on every request, resolves the user and their permissions. Permissions are granular per-entity (events_view, events_create, etc.) and can be global or chapter-scoped. Chapter-scoped view/read permissions automatically extend to child chapters. Write permissions are always explicit per-chapter. The Blazor app stores the token in localStorage, adds it as a Bearer header, and redirects to login on 401.

**Tech Stack:** Existing Token/Permission infrastructure, FastEndpoints preprocessors, Blazor localStorage (via JS interop), `Authorization: Bearer` header

---

## Design Decisions (from user)

1. **Bearer token** — `Authorization: Bearer <token>` header (not cookies, not custom headers)
2. **Granular permissions** — per-entity CRUD, not role-based
3. **Hierarchical read** — view/read permissions on a parent chapter automatically apply to child chapters; write permissions are NEVER inherited
4. **Template roles** — future TODO (e.g., "chapter officer" auto-applies a set of permissions)
5. **Login UI** — two card buttons (SSO + manual), login button in nav, redirect back after login
6. **SAML** — leave as-is, implement later with real IdP
7. **Public endpoints** — motions (list/create), applications, due selection, chapters, admin divisions, login, SAML, antiforgery, config

---

## Permission Definitions

### Global permissions
| Identifier | Display Name | Description |
|---|---|---|
| `users_create` | Benutzer erstellen | Create user accounts (existing) |
| `users_view` | Benutzer anzeigen | View user list |
| `chapters_create` | Gliederungen erstellen | Create chapters (existing) |
| `options_view` | Einstellungen anzeigen | View system options |
| `options_edit` | Einstellungen bearbeiten | Edit system options |
| `audit_view` | Audit-Log anzeigen | View audit logs |
| `emaillogs_view` | E-Mail-Log anzeigen | View email sending logs |
| `member_import_trigger` | Mitgliederimport auslösen | Trigger manual member import |

### Chapter-scoped permissions
| Identifier | Display Name | Description |
|---|---|---|
| `applications_view` | Anträge anzeigen | View membership applications (existing) |
| `applications_process` | Anträge bearbeiten | Process applications (existing) |
| `dueselections_view` | Einstufungen anzeigen | View due selections (existing) |
| `dueselections_process` | Einstufungen bearbeiten | Process due selections (existing) |
| `events_view` | Events anzeigen | View events |
| `events_create` | Events erstellen | Create events |
| `events_edit` | Events bearbeiten | Edit events, checklist items |
| `events_delete` | Events löschen | Archive/delete events |
| `motions_view` | Anträge (Vorstand) anzeigen | View motions in admin |
| `motions_edit` | Anträge bearbeiten | Edit motion status |
| `motions_vote` | Abstimmen | Cast votes on motions |
| `members_view` | Mitglieder anzeigen | View member list and details |
| `members_edit` | Mitglieder bearbeiten | Edit member data |
| `officers_view` | Vorstand anzeigen | View chapter officers |
| `officers_edit` | Vorstand bearbeiten | Add/remove chapter officers |
| `templates_view` | Vorlagen anzeigen | View event templates |
| `templates_edit` | Vorlagen bearbeiten | Create/delete event templates |

---

## Endpoint Classification

### Anonymous (no auth required)
- `POST /api/users/login`
- `GET /api/users/SamlLoginStart`, `POST /api/users/SamlConsume`
- `GET /api/antiforgery/token`
- `GET /api/config/client`
- `POST /api/motions` (public motion submission)
- `GET /api/motions`, `GET /api/motions/{id}` (public motion listing)
- `POST /api/membershipapplications`
- `POST /api/dueselector`
- `GET /api/chapters/*` (public chapter info)
- `GET /api/administrativedivisions/*`
- `GET /api/testdata/seed` (DEBUG only)

### Authenticated (any logged-in user)
- `GET /api/config/client` (could optionally return more data when authenticated)

### Permission-gated (specific permission required)
- All admin endpoints — mapped to their respective permission identifiers
- Chapter-scoped endpoints resolve ChapterId from request and check hierarchical permissions

---

## Tasks

### Task 1: Permission definitions and seed data

**Scope:** Expand `PermissionIdentifier.cs` with all new identifiers, update `PermissionRepository.SupplementDefaults()` to seed them, add to M001 migration if needed.

**Files:**
- Modify: `Quartermaster.Api/PermissionIdentifier.cs`
- Modify: `Quartermaster.Data/Permissions/PermissionRepository.cs`

### Task 2: Fix TokenAuthenticationHandler for Bearer tokens

**Scope:** Rewrite `TokenAuthenticationHandler` to read `Authorization: Bearer <token>` header (not custom headers). Validate token against DB, check expiry, populate claims (UserId, permissions). Wire `UseAuthentication()` into Program.cs pipeline.

**Files:**
- Modify: `Quartermaster.Server/Authentication/TokenAuthenticationHandler.cs`
- Modify: `Quartermaster.Server/Program.cs`
- Modify: `Quartermaster.Server/Users/LoginEndpoint.cs` — return token content + user info in response

### Task 3: Authorization preprocessor

**Scope:** Create a FastEndpoints global preprocessor that:
1. Checks if endpoint requires auth (not in anonymous list)
2. Extracts user from authenticated principal
3. For permission-gated endpoints: checks if user has required permission
4. For chapter-scoped permissions: resolves ChapterId from request, checks permission with hierarchical read inheritance (walk ancestor chain)
5. Returns 401 for unauthenticated, 403 for unauthorized

**Files:**
- Create: `Quartermaster.Server/Authentication/AuthorizationPreProcessor.cs`
- Create: `Quartermaster.Server/Authentication/RequirePermissionAttribute.cs` — custom attribute for endpoints
- Modify: All admin endpoint `Configure()` methods — remove `AllowAnonymous()`, add permission requirement

### Task 4: Hierarchical permission checking

**Scope:** Update `UserChapterPermissionRepository` to support hierarchical read. When checking a view/read permission for a chapter, walk the ancestor chain — if the user has the permission on ANY ancestor, grant access. Write permissions are always exact-match only.

**Files:**
- Modify: `Quartermaster.Data/UserChapterPermissions/UserChapterPermissionRepository.cs`
- Possibly modify: `Quartermaster.Data/Chapters/ChapterRepository.cs` (if GetAncestorChain needs adjustments)

### Task 5: Blazor auth state and token management

**Scope:** Create a Blazor auth state provider that:
1. Stores Bearer token in localStorage via JS interop
2. Adds `Authorization: Bearer <token>` header to all HTTP requests
3. Exposes auth state (isAuthenticated, current user, permissions)
4. Redirects to login on 401 responses
5. Redirects back to original page after login

**Files:**
- Create: `Quartermaster.Blazor/Services/AuthService.cs` — token storage, login/logout, user state
- Create: `Quartermaster.Blazor/wwwroot/js/auth.js` — localStorage interop
- Modify: `Quartermaster.Blazor/Http/CsrfDelegatingHandler.cs` — also add Bearer header
- Modify: `Quartermaster.Blazor/Program.cs` — register AuthService

### Task 6: Login UI

**Scope:** Create login page with two card buttons (SSO + manual login). Add login button to nav bar. Manual login form with username/password. SSO button checks if SAML is configured (from ClientConfig), disabled if not.

**Files:**
- Create: `Quartermaster.Blazor/Pages/Login.razor` + `.cs` — login page
- Create: `Quartermaster.Blazor/Pages/LoginManual.razor` + `.cs` — username/password form
- Modify: `Quartermaster.Blazor/Layout/MainLayout.razor` — add login/logout button in nav
- Modify: `Quartermaster.Api/Config/ClientConfigDTO.cs` — add `SamlEnabled` and `SamlButtonText` fields
- Modify: `Quartermaster.Server/Config/ClientConfigEndpoint.cs` — populate SAML fields

### Task 7: Wire authenticated user into audit log

**Scope:** Replace all "System" TODO markers with actual authenticated user. The auth preprocessor resolves the user — pass it through to repositories via a scoped service or HttpContext accessor.

**Files:**
- Create: `Quartermaster.Server/Authentication/CurrentUserService.cs` — scoped service holding current user
- Modify: `Quartermaster.Data/AuditLog/AuditLogRepository.cs` — accept user info
- Modify: All repositories with TODO markers (7 files)

### Task 8: Admin UI for user permission management

**Scope:** Create pages where admins can view users and manage their permissions (grant/revoke global and chapter-scoped permissions).

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/UserDetail.razor` + `.cs`
- Create: `Quartermaster.Server/Users/UserPermissionEndpoints.cs` — CRUD for user permissions
- Modify: Existing user list page to link to detail

### Task 9: Drop DB, rebuild, test, verify

**Scope:** Reset database, verify all tests pass, verify login flow in Chrome, verify permission enforcement.

---

## Future TODOs (noted, not implemented now)

- Template roles (e.g., "Chapter Officer" = auto-apply a set of permissions)
- SAML login flow completion (match SAML identity to user, create session)
- Token refresh mechanism (ExtendType field exists but unused)
- Password reset flow
- Session management UI (view/revoke active tokens)

---

## Execution Notes

- Tasks 1-4 are server-side foundation — must be done in order
- Task 5-6 are client-side — can be done in parallel with 7-8
- Task 7-8 depend on tasks 1-4
- Task 9 is final verification
- This is the largest feature — expect 2-3 sessions to complete
- Each task should be verified independently before moving to the next
