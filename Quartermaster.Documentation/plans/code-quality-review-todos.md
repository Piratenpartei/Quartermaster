# Code Quality Review TODOs — 2026-05-22

Findings from a full-codebase parallel review across the five projects (Api, Data, Server, Blazor, Server.Tests). Reviewed against `CLAUDE.md` rules plus general security/quality concerns. Items are grouped by priority; each entry has a file:line anchor so it can be picked up cold.

---

## Critical (Must-Fix Before Production)

### Authentication & Token Model

- [x] **Login tokens never expire.** Fixed: `TokenRepository.LoginUser` now sets `Expires = UtcNow + auth.token.lifetime_days` (new global option, default 7) on issue, and `ValidateLoginToken` slides expiry forward by the same window on each successful validate (sliding-window, ExtendType.Usage). Expiration policy moved out of the static `TokenExtensions` entity layer into `TokenRepository` (resolves the in-code TODO). The dead static `CheckLoginToken`/`CheckSimpleToken` helpers + their `UpdateTokenExpiration` were subsequently removed as part of the TokenSecurityScope cleanup (next item). Added `Issued_token_has_expiry_populated_within_configured_lifetime` + `Successful_validation_slides_expiry_forward` tests; full 939-test suite green.
- [x] **`TokenSecurityScope` (None / IP / BrowserFingerprint) is dead.** Decided against IP binding (mobile UX cost too high relative to the threats it'd catch — XSS, the dominant theft vector, isn't mitigated by IP binding since the attacker proxies through the victim's browser). Instead: deleted the entire `TokenSecurityScope` enum + column (folded into M001 — pre-prod migration window) + the dead static `CheckLoginToken`/`CheckSimpleToken`/`UpdateTokenExpiration`/`GenerateLoginTokenIP` helpers + the unused `TokenRepository.CheckLoginToken`/`CheckToken` wrappers + the `fingerprint` parameter on `LoginUser`. Added forensic-only audit columns to `Token`: `IssuedAt` (defaults to UtcNow), `IssuedIp` (45-char nullable, IPv6-sized), `IssuedUserAgent` (512-char nullable). Both manual login (`LoginEndpoint`) and SSO paths (`SsoLoginHelper` → `SamlLoginConsumeEndpoint`, `OidcCallbackEndpoint`) populate them. Added `Successful_login_records_issuer_ip_and_user_agent_on_token` test. The audit columns are the data backing for the new "Active Sessions UI" feature item now tracked in [`feature-todos.md`](./feature-todos.md). Full 939-test suite green (incl. all E2E).
- [x] **`X-Forwarded-For` trusted unconditionally for lockout keying.** Fixed: added `ForwardedHeadersSettings` (`Quartermaster.Server/Security/`) bound from the `ForwardedHeaders` config section, with `KnownProxies` (IPs) + `KnownNetworks` (CIDRs). Both default empty ⇒ `X-Forwarded-*` is ignored entirely and `Connection.RemoteIpAddress` is authoritative. When the deployer populates either list, `app.UseForwardedHeaders(...)` runs before authentication and unwraps the chain for the trusted hops only. Deleted `LoginEndpoint.GetClientIp` — call sites now read `HttpContext.Connection.RemoteIpAddress` directly, getting the real client IP behind a configured proxy or the connection IP otherwise. `appsettings.template.json` documents the new section. Two new tests: `Rotating_X_Forwarded_For_does_not_bypass_per_IP_lockout` (security: spoofed X-F-F can't dodge the per-IP lockout when no proxy is trusted) + `Trusted_proxy_honors_X_Forwarded_For_so_distinct_real_ips_get_distinct_lockout_buckets` (happy path: when 127.0.0.1 is in `KnownProxies`, distinct forwarded IPs do get distinct buckets, so two different real users behind the proxy aren't unfairly cross-locked). Full 941-test suite green.
- [x] **Delete `EndpointProcessors/ChapterPermissionRequirement.cs` and `EndpointProcessors/ChapterRequirement.cs`.** Both deleted along with the now-orphan `Quartermaster.Data/Chapters/IChapterIdentifier.cs` interface (only those two processors referenced it). Empty `EndpointProcessors/` directory removed.

### Authorization & IDOR

- [x] **Permission inheritance is silently disabled across the system.** False positive — the review confused property *names* (`ViewEvents`) with *values* (`"events_view"`). The `EndsWith("_view")` check matches the seeded identifier convention. Added a convention-pin test (`Inheritance_is_decided_by_underscore_view_suffix_not_by_pascalcase_name`) using synthetic `synthetic_view` / `synthetic_view_all` permissions, and an XML doc on `IsViewPermission` flagging the suffix dependency.
- [x] **`ChapterOfficerAddEndpoint` doesn't verify member↔chapter.** Endpoint now fetches the member BEFORE creating the officer row and rejects with `error.chapter.officer.member_not_found` (member missing) or `error.chapter.officer.member_chapter_mismatch` (member belongs to a different chapter). Two regression tests added: `Rejects_member_from_a_different_chapter_and_does_not_create_officer_or_grant_permissions` + `Rejects_nonexistent_member`.
- [x] **`MembershipApplicationRepository.List` widens on empty `chapterIds`.** Confirmed as a real privilege-escalation bug: a chapter-scoped user requesting `?ChapterId={non-permitted}` got the empty-intersection passed to the repo, which then dropped the filter and returned ALL applications. Changed `List(...)` to take `List<Guid>?`: `null` = no filter (global view only), non-null (incl. empty) = exactly these chapters. Endpoint updated to pass `null` only on the global path. Added `Chapter_viewer_requesting_non_permitted_chapter_gets_empty_not_widened` regression test.

### Secret Exposure

- [x] **`OptionListEndpoint` returns all secrets in cleartext to any `ViewOptions` holder.** Added `IsSecret` column on `OptionDefinition` (M001 fold), marked the three known secrets (`email.smtp.password`, `auth.oidc.client_secret`, `auth.saml.certificate`). New global permission `ViewOptionSecrets` ("Einstellungen: Geheimnisse im Klartext anzeigen") — auto-granted to the root admin via the existing `SupplementDefaults` global-perm seeding. `OptionListEndpoint` masks secret values with `••••••` for callers lacking the new perm; DTO carries `IsSecret` + `ValueMasked` so the frontend can render a secret-aware input. `OptionRepository.SetValue` redacts old/new values in the audit log for secret options (the real value is still persisted — SMTP/OIDC/SAML consumers still resolve plaintext via `ResolveValue`). Tests: secret masked without perm, plaintext with perm, non-secret never masked, audit log redaction for secret writes.
- [ ] **Bearer token stored in localStorage.** `Quartermaster.Blazor/wwwroot/js/auth.js:1-7`. Accessible to any XSS. Existing decision may be intentional given Blazor WASM constraints, but pair with a real XSS audit before going public.

### SAML / OIDC Hardening

- [x] **SAML signature has no replay / audience / NotBefore-NotOnOrAfter checks.** Added `SamlAssertionParser` (raw-XML metadata extraction) and `UsedSamlAssertion` replay table (M001 fold). After `IsValid()`, consume endpoint now enforces: timestamps within `NotBefore`/`NotOnOrAfter` ±60s clock skew; audience matches `auth.saml.expected_audience` if configured; destination matches `auth.saml.expected_destination` if configured; `AssertionID` not previously seen (rejected via `UsedSamlAssertionRepository.TryMarkUsed`). Replay window is the assertion's own `NotOnOrAfter`; rows are lazily pruned past that. Two new options seeded (audience/destination — empty by default, skipping the check when unset since most prod IdPs won't be configured day one). Parser unit tests (8: happy path + every parse-failure mode) + repo tests (4: first-use, replay, prune, distinct).
- [ ] **OIDC: new `HttpClient` per callback + discovery fetched on every call.** `Quartermaster.Server/Users/OidcCallbackEndpoint.cs:79,143`. Move to `IHttpClientFactory` and cache discovery.
- [ ] **OIDC: `state` parameter unused, `nonce`/`at_hash` not validated.** `Quartermaster.Server/Users/OidcCallbackEndpoint.cs:139-170`. CSRF on the redirect relies only on the `oidc_cv` (PKCE) cookie. Add `state` round-trip and validate `nonce`/`at_hash`.
- [x] **Scrub emails from auth-flow logs.** Both log statements now emit only the email domain (`email.Split('@').LastOrDefault() ?? "(unknown)"`) — keeps forensic signal for "is this an external IdP source?" without leaking the local-part / user identity.

### Anonymous POST Spam / DoS Surface

- [x] **Rate-limit anonymous create endpoints.** Added ASP.NET Core `AddRateLimiter` with a per-IP fixed-window policy `anonymous-create`, applied to `MotionCreate`, `MembershipApplicationCreate`, `DueSelectionCreate` via `Options(b => b.RequireRateLimiting(...))` — single shared bucket stops cross-endpoint amplification. Middleware ordered after `UseRouting` so endpoint metadata is visible. Configurable via DB Options (`auth.ratelimit.anonymous_create_permits`, default 5; `auth.ratelimit.anonymous_create_window_minutes`, default 10) — admin-tunable at runtime, takes effect for new IP partitions immediately and active ones after their window resets. Resolver guards against missing/unparseable/non-positive values with hardcoded fallback constants (5/10) so a fat-fingered admin can't silently disable signups. `IntegrationTestBase` bumps the per-worker DB value to 10000 (direct write, skipping audit log) so unrelated tests don't drain each other; two dedicated tests use `WithWebHostBuilder` for a fresh limiter and overwrite the Option directly before firing requests.

### Data Integrity (Pre-Production Migration Window)

- [x] **Composite-permission tables have no PK / no unique constraint.** Added composite PKs in M001 for `UserGlobalPermissions(UserId, PermissionId)`, `UserChapterPermissions(UserId, ChapterId, PermissionId)`, `RolePermissions(RoleId, PermissionIdentifier)`, `ChapterAssociates(MemberId, ChapterId, AssociateType)`. Added `[PrimaryKey(Order = N)]` LinqToDB attrs on the four entities. Dropped the now-redundant `IX_RolePermissions_RoleId` (covered by the PK's leading column); kept `IX_ChapterAssociates_ChapterId` since `ChapterId` isn't the leading column of the composite PK. App-level pre-checks in `AddForUser`/`SetPermissions` remain as the first line of defense — the DB constraint is the integrity backstop closing the pre-check race.
- [x] **Multi-write operations lack transactions** *(focused-scope only — high-write hotspots)*. Wrapped `MotionRepository.TryAutoResolve` (5+ writes across Motion + MembershipApplication + DueSelection), `RoleRepository.Delete` (3 cascading deletes), `RoleRepository.SetPermissions` (delete-then-N-inserts), and `OptionRepository.SetValue` (write + 1-2 audit inserts) in `using var tx = _context.BeginTransaction(); ... tx.Commit();`. Verified no caller stitches multiple newly-transactional methods together (LinqToDB throws on nested `BeginTransaction`). Tests rely on existing coverage + the SQL primitive — no new rollback-injection tests.

  **Deferred (low-risk):** the mechanical sweep across every `Update(...)` that pairs `Set + LogFieldChange` in the 10 audit-emitting repos. Partial-failure mode is "field updated but audit row missing" — annoying but not data-corrupting. Worth a separate pass when the repo layer gets a broader cleanup; would benefit from a `BeginOrJoinTransaction` helper to safely cover cross-method endpoint orchestration.
- [ ] **Decide on soft delete consistency.** Present on User/Event/Meeting/Motion/MembershipApplication/DueSelection/EventTemplate; missing on Member/Chapter/Role/Token/AdministrativeDivision/EmailLog/AgendaItem/CollabDocument/ChapterOfficer + all permission tables. Member especially is heavily referenced; either add `DeletedAt` or document the deliberate hard-delete decision.

---

## High Priority (Functional Bugs / Significant Quality Concerns)

### Public Wire Contract Bugs

- [ ] **Typo on a public enum: `PaymentScedule`** (missing `h`). `Quartermaster.Api/DueSelector/DueSelectionDTO.cs:22,38`, propagated through Blazor. Meanwhile `DueSelectionDetailDTO.cs:27` uses the correct `PaymentSchedule`. Fix the typo and the asymmetry — coordinate with any external clients.
- [ ] **Typo on production class: `PasswordHashser`.** `Quartermaster.Data/PasswordHasher.cs:7`, referenced from `UserRepository.cs:69`, `Server/Users/LoginEndpoint.cs:64`, `Tests/Infrastructure/TestDataBuilder.cs:125`. Rename.
- [ ] **`Status` / `Type` / `Vote` typed as `int` on 15+ DTOs** despite proper enums existing in the same project. Examples: `MembershipApplicationAdminDTO.Status`, `MotionDTO.ApprovalStatus`, `MotionVoteRequest.Vote`, `AgendaItemDTO.MotionApprovalStatus`, `ChapterOfficerDTO.AssociateType`, `EventChecklistItemDTO.ItemType`, `RoleDTO.Scope`, `UserRoleAssignmentDTO.RoleScope`. Switch to enums. Also see `feedback_enum_persistence_order.md` — reordering enums corrupts persisted records.
- [ ] **Inconsistent pagination response shape.** Some include `Page`/`PageSize` echo (`MeetingListResponse`, `MotionListResponse`, `DueSelectionListResponse`, `MembershipApplicationListResponse`, `AdministrativeDivisionSearchResponse`), others omit (`EventSearchResponse`, `MemberSearchResponse`, `ChapterOfficerSearchResponse`, `ChapterSearchResponse`, `MemberImportLogListResponse`, `AdminDivisionImportLogListResponse`, `DashboardSection<T>`). Introduce `IPaginatedResponse<T>` matching `IPaginatedRequest`.
- [ ] **Raw JSON strings leak persistence into wire contracts.** `Quartermaster.Api/Events/EventTemplateDetailDTO.cs:10-11` (`Variables`, `ChecklistItemTemplates`) and `EventChecklistItemDTO.cs:13` (`Configuration`) — model the nested structure on the wire instead.

### N+1 Query Patterns

- [ ] **`UserChapterPermissionRepository.HasPermissionWithInheritance:47-52`** — calls `GetAncestorChain` (N queries) then `HasInheritablePermissionForChapter` (2 more queries each) per ancestor. O(chain_length × 4) round-trips on the permission hot path. Rewrite as one recursive CTE.
- [ ] **`ChapterRepository.GetAncestorChain:99-109`** — one SELECT per ancestor.
- [ ] **`AdministrativeDivisionRepository.GetAncestorIds` (:89-99) and `GetDescendantIds` (:68-87)** — same N+1.
- [ ] **`AgendaItemRepository.GetDepth` and `WouldCreateCycle` (:38-67)** — one SELECT per level.
- [ ] **`OptionRepository.ResolveValue:41-48`** — one SELECT per ancestor chapter.

### Bare Catches / Swallowed Exceptions

- [ ] **5 bare-catch sites in Server** (CLAUDE.md hard rule): `Email/EmailSendingBackgroundService.cs:141`, `Events/EventTemplateListEndpoint.cs:63,72`, `Meetings/AgendaItemPresenceEndpoint.cs:91`, `Meetings/MeetingHub.cs:348`, `Meetings/MeetingDetailEndpoint.cs:134`. Catch `Exception ex` and log `{ex}`.
- [ ] **17 bare-catch / swallow sites in Blazor.** Notable: `Services/AuthService.cs:60` (invalid stored token leaves user unauth'd but doesn't clear localStorage → reload loop), `LoginSamlCallback.razor.cs`, `MeetingLive.razor.cs:62` (live updates silently disabled). Full list:
  - `Program.cs:31`, `Services/AuthService.cs:60`, `Services/ClientConfigService.cs:31`
  - `Components/Inputs/CodeMirrorEditor.razor.cs:219,240,280,302,307`
  - `Components/Inputs/MotionPicker.razor.cs:67`
  - `Pages/LoginManual.razor.cs:40`, `Pages/UserSettings.razor.cs:31`
  - `Pages/Administration/EventDetail.razor.cs:412,423`
  - `Pages/Administration/ImportStatus.razor.cs:54`
  - `Pages/Administration/MeetingLive.razor.cs:62,100,131`
  - `Pages/Administration/MemberImportHistory.razor.cs:139`
- [ ] **`ex.Message` instead of `{ex}` (drops stack + inner exceptions):** `AdministrativeDivisions/AdminDivisionImportService.cs:71`, `Email/EmailSendingBackgroundService.cs:128,136`.

### Blazor Hard Violations & Quality

- [ ] **Convert remaining `@code` blocks** (CLAUDE.md hard rule, only 2 of 97 components): `Blazor/Pages/Administration/ChapterTreeNode.razor:37` and `Blazor/Pages/Administration/TreeNode.razor:38` — extract to `.razor.cs`.
- [ ] **`MainLayout` may leak handlers.** `Blazor/Layout/MainLayout.razor.cs:57` has `Dispose()` but verify the `.razor` declares `IDisposable`. Static event `AuthService.OnTokenExpired` accumulates handlers if not unhooked.
- [ ] **`AuthService._initTcs` re-assigned on logout.** `Quartermaster.Blazor/Services/AuthService.cs:102` — anyone awaiting the previous TCS hangs forever. Use a single long-lived TCS or replace with a `SemaphoreSlim`.
- [ ] **Anonymous-typed request bodies bypass typed `Quartermaster.Api` contracts.** `Pages/Administration/EventDetail.razor.cs:327`, `MeetingLive.razor.cs:248`, `UserDetail.razor.cs`. Use the typed Request DTOs.
- [ ] **Hardcoded German UI strings in `.razor.cs`** in 25+ sites (`MeetingLive.razor.cs:193,201,207,238`, `Components/PageBackLink.razor.cs:16`, `Services/ToastService.cs:34,42,52`, …). `I18nService` exists — route these through it.

### Server Quality

- [ ] **`EmailService.SendEmail` blocks on `renderTask.Wait()`.** `Quartermaster.Server/Email/EmailService.cs:107-112`. Make async — currently stalls a thread per email and can deadlock.
- [ ] **`HandleFailure` re-enqueues via `await Task.Delay(10 × attemptCount)` inline.** `Quartermaster.Server/Email/EmailSendingBackgroundService.cs` — blocks the consumer loop. Use a scheduled re-enqueue (dedicated retry channel).
- [ ] **`MemberImportHostedService` busy-polls `HasCompletedInitialLoad`** at 1 s. Use a `TaskCompletionSource`/`SemaphoreSlim`.
- [ ] **`Rendering/` namespace lives in `Quartermaster.Api`** dragging `Fluid.Core`, `HtmlSanitizer`, `Markdig`, plus German mock data into the contract assembly that Blazor WASM also references. Move to `Quartermaster.Server`.

### Data Layer Quality

- [ ] **`SupplementDefaultPermission` is dead code.** `Quartermaster.Data/Users/UserRepository.cs:62-64`.
- [ ] **`SqlContext.cs` is fully commented-out dead code.** Delete `Quartermaster.Data/SqlContext.cs`. Drop unused `Microsoft.Data.Sqlite` + `InterpolatedSql.Dapper` package refs in `Quartermaster.Data.csproj`.
- [ ] **`AddRootAccount` creates a `User` with most NOT NULL fields defaulting.** `Quartermaster.Data/Users/UserRepository.cs:67-70` — relies on `Guid.Empty` linking to a seeded "Null Island" admin division. Either fail-fast or document the seed dependency.
- [ ] **`EnsureSetGuid` / `ThrowOnEmptyGuid` unused.** `Quartermaster.Data/Abstract/RepositoryBase.cs:8-23`.
- [ ] **`AuditLog.AuditLog` namespace=type collision** forces fully-qualified references at `Quartermaster.Data/DbContext.cs:49` and 14 sites in `M001_InitialStructureMigration.cs`. Rename the entity to `AuditEntry` per CLAUDE.md rule.
- [ ] **Audit-log mapping inconsistency.** `Members/MemberRepository.cs:142-173` (`LogMemberFieldChanges`, 30+ lines) vs. `EventRepository.Update:77-83` vs. `MeetingRepository.Update:66-76` — pick one shape. A generic reflection-driven diff (kept inline per the no-mapper rule) would eliminate the silent-drift risk when new fields are added.
- [ ] **Pre-fetch `oldValue` is outside the update transaction.** `Quartermaster.Data/MembershipApplications/MembershipApplicationRepository.cs:51-60`, `DueSelectionRepository.cs:56-65`. Under concurrent updates the audit log records stale `oldValue`. Pull into the same transaction as the wrapping fix above.
- [ ] **Repository shape drift.** Some `Get(Guid)` filter `DeletedAt == null` (User, Event, Meeting, Motion, Membership, DueSelection); others don't (Member, Chapter, AdministrativeDivision, Role, Permission, Token, EmailLog, AgendaItem, CollabDocument). Naming drift too: `Get` vs `GetById` vs `FindBy*`. Standardize.
- [ ] **`AddIfNotExists`-style seeding repeats in 4 repos** (Permission, Role, Option, AdminDivision) and races without a unique index. Add the index, or abstract once.

### Test Quality & Coverage

- [ ] **Coverage gap: entire vote lifecycle on agenda items** is untested. Add tests for `AgendaItemVoteEndpoint`, `AgendaItemCloseVoteEndpoint`, `AgendaItemReopenEndpoint`, `AgendaItemImportMotionsEndpoint`, `AgendaItemPresenceEndpoint`.
- [ ] **Coverage gap: SSO/SAML/OIDC entirely untested** — `OidcLoginStartEndpoint`, `OidcCallbackEndpoint`, `SamlLoginStartEndpoint`, `SamlLoginConsumeEndpoint`. Security-critical; add at minimum auth-error path + happy-path tests.
- [ ] **Coverage gap: security plumbing untested** — `TokenAuthenticationHandler`, `AntiforgeryMiddleware`, the endpoint processors. Direct unit tests instead of indirect smoke.
- [ ] **Coverage gap: 10+ meeting/chapter validators untested.** Listed in tests review.
- [ ] **E2E factory leaks Kestrel hosts.** `Tests/Infrastructure/E2ETestBase.cs:77,90-99` — fresh `WebApplicationFactory` per test, `[After(Test)]` doesn't dispose it. Move to a per-worker shared factory like `IntegrationTestFactory`.
- [ ] **Collab E2E tests don't actually distinguish authors.** `Tests/E2E/CollabEditorE2ETests.cs:270,318,367,514,540` use default seeded name `"Test User"` for both Alice and Bob; `"Geschrieben von Test User"` marker assertions pass spuriously. Override `firstName`/`lastName` per-author in setup.
- [ ] **Drop 33 s of hardcoded sleeps in `CollabEditorE2ETests`** waiting on the snapshot timer. Expose a trigger hook or call `SaveSnapshot` directly.
- [ ] **Consolidate per-test DB setup boilerplate.** Multiple test classes reimplement worker-DB acquisition instead of inheriting from `IntegrationTestBase`: `Authentication/EndpointAuthorizationHelperTests`, `LoginAttempts/LockoutLogicTests`, `Meetings/MeetingRepositoryTests`, `Permissions/PermissionInheritanceTests`, `Motions/MotionRepositoryTests` (+ 3 more).
- [ ] **Hack: `MeetingListEndpointTests:43-47,58-62` fires a throwaway request to force role seeding.** Confirm seed order in `IntegrationTestBase.SupplementDefaults()` and remove the workaround.
- [ ] **`LoginAttempts/LockoutLogicTests.cs:59`** uses `await Task.Delay(10)` then compares against `DateTime.UtcNow.AddMilliseconds(-5)`. Racy. Inject a clock or use `FakeTimeProvider`.
- [ ] **`E2ESmokeTests.cs:22,29`** throws generic `Exception` — use specific type.
- [ ] **Random `MemberNumber` in `TestDataBuilder.cs:131-132`** has no uniqueness check; rare flake under contention.

---

## Medium Priority (Style / Consistency)

### CLAUDE.md Violations

- [ ] **Extracted `*DtoBuilder` mapper classes** (rule: hand-written inline mapping at call site). Inline and delete: `Server/Roles/RoleDtoBuilder.cs` (called from `RoleCreateEndpoint:69`, `RoleListEndpoint:39`) and `Server/Meetings/MeetingDtoBuilder.cs` (called from `MeetingDetailEndpoint:147`, `MeetingProtocolEndpoint:146`, `MeetingListEndpoint:184`, `MeetingCreateEndpoint:64`, `MeetingLifecycleService:167`).
- [ ] **`using` aliases** (rule: rename instead of alias):
  - `Server/DueSelector/DueSelectionCreateEndpoint.cs:5` — `using DataDueSelector = Quartermaster.Data.DueSelector;`
  - `Blazor/Pages/DueSelector/DueSelectorEntryState.cs:2` — `using ApiDueSelector = Quartermaster.Api.DueSelector;`
- [ ] **Fully-qualified types despite available `using` directives.** Heavy in `Server/Program.cs:26,89-156` and `Server/Meetings/MeetingDetailEndpoint.cs:104-134`. Also `Api/I18n/I18nService.cs:78`, `Api/Meetings/MeetingHubMessages.cs:36,46,49`, `Api/Meetings/MeetingRequests.cs:39`, `Data/Migrations/M001_InitialStructureMigration.cs` (multiple `System.Data.Rule.*`, fix with `using System.Data;`), `Tests/Infrastructure/IntegrationTestBase.cs:35-37`, `Tests/Infrastructure/E2ETestBase.cs:18`, `Tests/E2E/CollabEditorE2ETests.cs:90-158` (`System.Text.Json.JsonSerializer`).
- [ ] **Region-separator comments** (banned):
  - `Data/Permissions/PermissionRepository.cs:27,41` (`// Global permissions` / `// Chapter-scoped permissions`)
  - `Data/Roles/RoleRepository.cs:48,67`
  - `Blazor/Pages/Administration/EventDetail.razor.cs:41,45,57,151,184,213` (6 section labels in one 429-line file)
  - `Blazor/Components/Inputs/CodeMirrorEditor.razor.cs:47`
  - `Tests/E2E/CollabEditorE2ETests.cs:30,178,563`
  - `Api/DueSelector/DueSelectionDetailDTO.cs:8-33`
  - `Api/MembershipApplications/MembershipApplicationDetailDTO.cs:9-44`
  - `Api/Meetings/MeetingHubMessages.cs:15-16`
- [ ] **`object _factoryLock = new();`** — use `System.Threading.Lock` (.NET 10). `Tests/Infrastructure/TestDatabaseFixture.cs:92`.
- [ ] **Mixed tabs/spaces** in `Data/Tokens/TokenRepository.cs` and `Data/Permissions/PermissionRepository.cs`.
- [ ] **`Where(predicate).Count()`** at `Data/ChapterAssociates/ChapterOfficerRepository.cs:28`, `Tests/Integration/Events/ChecklistItemDeleteEndpointTests.cs:53`, `Tests/Integration/Events/EventTemplateDeleteEndpointTests.cs:51`.
- [ ] **Field typo `_contenxt`** at `Data/Tokens/TokenRepository.cs:11,14,17,20,23,26,40`.
- [ ] **One-class-per-file violations**:
  - `Tests/Infrastructure/TestDatabaseFixture.cs:20,80` — two logic-bearing classes (`TestDatabaseFixture` + `WorkerDatabase`).
  - `Api/Meetings/MeetingRequests.cs` — 9 request classes bundled.
  - `Api/Users/LoginLockoutDTO.cs`, `Api/Users/UserPermissionsDTO.cs`, `Api/Roles/RoleDTO.cs` (5 unrelated types).
  - `Data/Users/LoginAttemptRepository.cs:70`.
- [ ] **Code on same line as `if`** (multi-line condition path): `Server/Meetings/MeetingProtocolEndpoint.cs:137`, `Server/Meetings/MeetingLifecycleService.cs:158`, `Server/Meetings/ProtocolPdfRenderer.cs:63`, `Tests/Infrastructure/E2ETestBase.cs:96-97`.
- [ ] **Narration-only comments** restating code: `Data/Roles/RoleRepository.cs:48,67`, `Api/Meetings/MeetingRequests.cs:85`, `Api/Meetings/AgendaItemDTO.cs:34` (the latter should be an enum, not a comment explaining the numeric encoding).
- [ ] **`E2ETestBase.cs:93`** swallows `ctx.CloseAsync()` exception with bare `catch { }`.
- [ ] **`E2ESmokeTests.cs:22,29`** throws generic `Exception`.
- [ ] **`MeetingRepositoryTests:30`** and **`MotionRepositoryTests:30`** use fully-qualified `Quartermaster.Data.Roles.RoleRepository`.
- [ ] **`MemberImportService.cs:42`** declares `int totalRecords = 0, newRecords = 0, updatedRecords = 0;` — violates "one statement per line" and "prefer `var`".

### Dead Code

- [ ] **7 dead types in `Quartermaster.Api`** (zero references solution-wide): `Options/SystemOptionDTO`, `Options/TemplatePreviewRequest`, `Options/TemplatePreviewResponse`, `Tokens/TokenDTO` (+ `ExtendTypeDTO`), `Users/UserPermissionsDTO.PermissionGrantRequest`, `Users/UserPermissionsDTO.ChapterPermissionGrantRequest`.
- [ ] **`Quartermaster.Server/TestData/TestDataSeedEndpoint.cs`** is dev-only but never invoked by tests. Move out of `Quartermaster.Server` or cover.
- [ ] **`I18nParams.cs:13` docstring references `I18nKey.Error.Meeting.StatusTransitionNotAllowed`** which doesn't exist (real key is `Error.Meeting.Status.TransitionInvalid`). Fix docstring or add the constant.

### Naming

- [ ] **`EMail` vs `Email`, `IBAN` vs `Iban`** inconsistencies across DTOs and i18n keys. Pick one spelling per term and align.
- [ ] **Login lockout responds 429 with empty body** (`Server/Users/LoginEndpoint.cs:46-49`). Add `Retry-After` header.
- [ ] **Duplicated `BuildDisplayName`** in `LoginEndpoint.cs:118-126`, `SessionEndpoint.cs:66-72`, `UserSettingsEndpoint.cs:112-118`. Extract.
- [ ] **Duplicated approve/deny/abstain switch logic** in `MeetingLifecycleService.cs:62-110` and `CloseVoteForAgendaItem`. Extract.

---

## Themes Worth a Dedicated Planning Pass

These don't fit a single TODO checkbox — each warrants its own plan document under `Quartermaster.Documentation/plans/`.

- [ ] **Auth audit & cleanup.** Concentrates the highest-risk findings (token expiry unimplemented, security scopes unimplemented, `X-Forwarded-For` trusted, IDOR in officer-add, secrets leaking from options, SAML hardening gaps, dead `EndpointProcessors`, OIDC hardening). Worth a focused branch rather than piecemeal fixes.
- [ ] **DTO contracts cleanup pass.** `PaymentScedule` rename, status enum typing, pagination shape, dead types, move `Rendering/` out of `Api`, JSON-string-on-DTO refactor.
- [ ] **Pre-production M001 fold-in.** Composite-key PKs, soft-delete decision, audit-log diff helper, transactions in multi-write paths — all benefit from being in the in-flight migration rather than M002.
- [ ] **Permission-check preprocessor.** ~30 endpoints repeat the same fetch-user → check-global → check-chapter → build-DTO boilerplate. Build a working `ChapterPermissionRequirement` (claims-based, not header-based) and adopt it. Replaces the dead processors that need to be deleted anyway.
- [ ] **Blazor architecture cleanup.** AuthService static-state untangle, EventDetail/MeetingDetail/MeetingLive split (200-430 lines each), typed API client wrapper to replace stringly-typed `HttpClient` calls, generic `LazyTreeNode<T>` to dedupe the three tree-page pairs.
