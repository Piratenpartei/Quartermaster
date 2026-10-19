# Discourse Forum Mirror Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mirror every submitted assembly motion as a Discourse topic in a per-portal category and keep its first post up to date.

**Architecture:** A forum sync next to the existing wiki sync under `Quartermaster.Server/AssemblyMotions/Forum/`, with its own state columns on `AssemblyMotions`, a minute-based hosted service, and the wiki's trigger points widened to queue both mirrors. An HTTP-only `IDiscourseClient` is swapped for a fake in tests.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator (MariaDB), Blazor WASM, TUnit.

**Spec:** `Quartermaster.Documentation/plans/2026-10-19-discourse-forum-mirror-design.md`

## Global Constraints

- Follow `CLAUDE.md` (K&R braces, single-statement if rule, guard-clause methods for 3+ conditions, explicit access modifiers, `var`, fluent LINQ, `System.Threading.Lock`, no `@code` in `.razor`, hand-written mapping, minimal comments).
- No git commands at all: no commit, add or status. Every task ends without a commit.
- Never contact a real Discourse from dev or tests; tests use `FakeDiscourseClient` or a fake `HttpMessageHandler`.
- No em or en dashes anywhere (code, i18n texts, docs).
- English identifiers and routes; German only in user-facing strings. Every new UI text is an i18n key in `I18nKey.cs`, `Quartermaster.Server/wwwroot/i18n/de.json` and `en.json`.
- Migration: file `Quartermaster.Data/Migrations/M006_1.cs`, `class M006_1`, `[Migration(009)]`. It is a new migration, so the dev server applies it on startup; no manual `ALTER TABLE` and never a drop.
- Build: `nice -n 10 dotnet build Quartermaster.sln -m:1 -v:q -nologo` from the repo root. Tests: from `Quartermaster.Server.Tests`, `dotnet run --no-build -- --treenode-filter "/*/<Namespace>/<Class>/*"`. Never run the full suite.
- Option keys: `discourse.base_url`, `discourse.api_username`, `discourse.api_key` (secret), `discourse.max_post_length` (default `32000`).
- External id: `qm-motion-{motionId}` (Guid "D" format).
- Topic title: `{DisplayNumber}: {Title} ({EventName})`, at most 255 characters, cut titles end with `...`.
- Batch: 10 motions per run, run every minute.

## Review Focus

- A motion title or text containing `@name`: no forum user is notified on create or on edit. Pinned in Task 4 (`Mentions_are_defused_everywhere`); the escape form is confirmed on the real forum in Task 10.
- A motion whose event name or title contains Markdown or `[details]`-like text (`[/details]` inside the reason): the collapsed blocks must not be closed early. Pinned in Task 4 (`Details_closing_tag_in_text_cannot_end_the_block`).
- A forum whose `max_post_length` is lower than our setting: the second attempt with notices succeeds and the motion is not left failing forever. Pinned in Task 5 (`Rejected_long_post_is_retried_with_notices`).
- A moderator deletes the topic, and the bot (a moderator) can still read it: the motion goes to Deleted and is not edited. Pinned in Task 5 (`Topic_with_deleted_at_marks_the_motion_deleted`).
- A wrong base URL or a revoked key: no motion is marked failed or Deleted because of it. Pinned in Task 5 (`Failed_session_check_touches_no_motion`).

---

### Task 1: Data model, migration and queuing

**Files:**
- Create: `Quartermaster.Api/AssemblyMotions/ForumSyncState.cs`
- Create: `Quartermaster.Data/Migrations/M006_1.cs`
- Modify: `Quartermaster.Data/AssemblyMotions/AssemblyMotion.cs`, `AssemblyMotionPortal.cs` (entity and `ToDto`)
- Modify: `Quartermaster.Data/AssemblyMotions/AssemblyMotionRepository.cs`
- Modify: every caller of `MarkWikiPending` except `AssemblyMotionWikiResync` (`AssemblyMotionService`, `AssemblyMotionCommissionService`, `AssemblyMotionCompetitionService`, `AssemblyMotionInquiryService`, `AssemblyMotionResultService`, `AssemblyMotionWikiReconcile`)
- Modify: `Quartermaster.Server/AssemblyMotions/AssemblyMotionService.cs` (`Join`, `Leave`)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionRepositoryTests.cs`, `AssemblyMotionServiceTests.cs`

**Interfaces:**
- Produces: `enum ForumSyncState { NotMirrored = 0, Pending = 1, Synced = 2, Failed = 3, Deleted = 4 }` in `Quartermaster.Api.AssemblyMotions`.
- Produces on `AssemblyMotion`: `int? ForumTopicId`, `int? ForumPostId`, `ForumSyncState ForumSyncState`, `int? ForumSyncedRevision`, `string? ForumSyncedHash`, `DateTime? ForumLastSyncedAt`, `string? ForumLastError`, `int ForumFailureCount`, `DateTime? ForumNextAttemptAt`, `DateTime? ForumSyncRequestedAt`.
- Produces on `AssemblyMotionPortal`: `bool ForumMirrorEnabled`, `int? ForumCategoryId` (also on `AssemblyMotionPortalDTO`).
- Produces on `AssemblyMotionRepository`:
  - `void MarkMirrorsPending(Guid motionId, DateTime nowUtc)` replaces `MarkWikiPending`; runs the wiki update (unchanged) and `MarkForumPending`.
  - `void MarkWikiPending(Guid motionId, DateTime nowUtc)` stays, used only by `AssemblyMotionWikiResync`.
  - `void MarkForumPending(Guid motionId, DateTime nowUtc)`: only while the portal has `ForumMirrorEnabled` and the state is not Deleted; sets Pending, `ForumSyncRequestedAt = nowUtc`, `ForumNextAttemptAt = null`, `ForumFailureCount = 0`.
  - `MarkWikiPendingAsNeverSynced` additionally calls `MarkForumPending`.
  - `void ResetForumPendingToNotMirrored(Guid portalId)`.
  - `List<AssemblyMotion> ListForumDue(DateTime nowUtc, int limit)`: portal `ForumMirrorEnabled`, `Number != null`, `DeletedAt == null`, state Pending or NotMirrored, or Failed with `ForumNextAttemptAt <= nowUtc`; ordered by `ForumSyncRequestedAt`.
  - `void SetForumTopic(Guid motionId, int topicId, int postId)` (unconditional).
  - `bool CompleteForumSync(Guid motionId, DateTime? loadedRequestedAt, int revision, string hash, DateTime nowUtc)`: same two-step conditional shape as `CompleteWikiSync`; state Synced, error null, count 0, next attempt null.
  - `bool FailForumSync(Guid motionId, DateTime? loadedRequestedAt, string error, int failureCount, DateTime nextAttemptAt)`: same shape as `FailWikiSync`.
  - `void MarkForumDeleted(Guid motionId, string error)` (unconditional; sets Deleted and the error).
  - `ForumLastError` is truncated to `WikiLastErrorMaxLength`.

- [ ] **Step 1: Write failing repository tests** in `AssemblyMotionRepositoryTests`:
  - `MarkMirrorsPending_queues_only_enabled_mirrors`: portal with wiki on and forum off → wiki Pending, forum NotMirrored; with both on → both Pending.
  - `MarkForumPending_leaves_deleted_motions_alone`: state Deleted stays Deleted, `ForumSyncRequestedAt` unchanged.
  - `ListForumDue_includes_not_mirrored_and_due_failures`: Pending, NotMirrored and Failed-due motions are listed; Failed-not-yet-due, Synced, Deleted, unnumbered and soft-deleted are not.
  - `CompleteForumSync_keeps_newer_request_pending`: `ForumSyncRequestedAt` changed after load → returns false, state stays Pending, `ForumSyncedHash` stored.
- [ ] **Step 2: Write failing service tests** in `AssemblyMotionServiceTests`: `Join_on_numbered_motion_queues_the_forum_only` and `Leave_on_numbered_motion_queues_the_forum_only` (forum Pending, wiki state unchanged); `Join_before_submission_queues_nothing`.
- [ ] **Step 3: Build and run both classes; expect compile failure.**
- [ ] **Step 4: Implement** the enum, entity properties (no column attributes needed, `IsColumnAttributeRequired = false`), `M006_1` (`Alter.Table(...).AddColumn(...)` per column; `ForumSyncState` as `AsInt32().NotNullable().WithDefaultValue(0)`, `ForumFailureCount` int default 0, `ForumMirrorEnabled` bool default false, `ForumSyncedHash` `AsString(64)`, `ForumLastError` `AsString(AssemblyMotionRepository.WikiLastErrorMaxLength)`; Down deletes them), the repository methods, the call-site rename, and `MarkForumPending` in `Join`/`Leave` after the submitter change when `motion.WasSubmitted`. Map the two portal fields in `AssemblyMotionPortal.ToDto`.
- [ ] **Step 5: Restart the dev server** (full solution build first, restart script in its own Bash call) and confirm in the log that migration 9 (`M006_1`) ran without errors.
- [ ] **Step 6: Build and run** `AssemblyMotionRepositoryTests`, `AssemblyMotionServiceTests`, `AssemblyMotionWikiSyncTests`; expect PASS.

### Task 2: Settings and options

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Forum/DiscourseSettings.cs`
- Modify: `Quartermaster.Data/Options/OptionRepository.cs` (after the `wiki.*` block)
- Modify: `Quartermaster.Blazor/Pages/Administration/Settings/SettingsAssistantCatalog.cs`, `Quartermaster.Api/Settings/SettingsCheckKind.cs`, `I18nKey.cs`, `de.json`, `en.json`
- Modify: `Quartermaster.Server/Program.cs` (scoped `DiscourseSettings.ReadFrom`, like line 168)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/DiscourseSettingsTests.cs`

**Interfaces:**
- Produces: `public record DiscourseSettings(string BaseUrl, string ApiUsername, string ApiKey, int MaxPostLength)` with `bool IsConfigured()` (all three strings set), `static bool IsSecureUrl(string url)` (delegate to `WikiBotSettings.IsSecureUrl`), `static DiscourseSettings ReadFrom(OptionRepository options)` (trims, strips a trailing `/` from the base URL, `MaxPostLength` falls back to 32000 when missing, unparsable or below 1000), `string TopicUrl(int topicId)` returning `{BaseUrl}/t/{topicId}`, and a `ToString` without the key.
- Produces: `SettingsCheckKind.Forum` (appended last).
- Produces: assistant `SettingsAssistantCatalog.Forum` ("Forum", `SettingsGroup.Integrations`, icon `bi-chat-square-text`, keys in order base_url, api_username, api_key, max_post_length, `Check = SettingsCheckKind.Forum`, `RequiredKeys` the first three), added to `All` after `Wiki`.

- [ ] **Step 1: Write failing tests** `DiscourseSettingsTests`: `ReadFrom_trims_and_strips_trailing_slash`, `MaxPostLength_falls_back_to_default` (empty, `abc`, `500` → 32000; `50000` → 50000), `ToString_never_contains_the_key`, `TopicUrl_uses_base_url`.
- [ ] **Step 2: Run, expect compile failure.**
- [ ] **Step 3: Implement** `DiscourseSettings`, the four option definitions (German labels "Forum: Basis-URL", "Forum: Bot-Benutzername", "Forum: API-Schlüssel" with `isSecret: true`, "Forum: Maximale Beitragslänge" as `OptionDataType.Number`, descriptions naming the moderator requirement and the Discourse setting `max_post_length`), the catalog entry and the i18n keys `Ui.Settings.ForumTitle`, `Ui.Settings.ForumDescription`.
- [ ] **Step 4: Run** `DiscourseSettingsTests` and `SettingsAssistantCatalogTests`; expect PASS.

### Task 3: Discourse HTTP client

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Forum/IDiscourseClient.cs`, `HttpDiscourseClient.cs`, `DiscourseException.cs`, `DiscourseTopic.cs`, `DiscoursePost.cs`, `DiscourseCurrentUser.cs`, `DiscourseCategory.cs`
- Modify: `Quartermaster.Server/Program.cs` (named client `"discourse"` with `SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = 5 min, UseCookies = false }`; singleton `IDiscourseClient`)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/HttpDiscourseClientTests.cs`, `Fakes/RecordingDiscourseHandler.cs`

**Interfaces:**
- Produces records (one file each or one data file, they are pure data): `DiscourseTopic(int TopicId, int FirstPostId, string Title, bool Deleted)`, `DiscoursePost(int PostId, string Raw, bool Deleted)`, `DiscourseCurrentUser(string Username, bool IsStaff)` (moderator or admin), `DiscourseCategory(int Id, string Name, bool ReadRestricted, bool CanCreateTopic)`.
- Produces `DiscourseException(string message, string code, int? statusCode = null, TimeSpan? retryAfter = null)` with codes as constants on the class: `Unauthorized` ("unauthorized", 401/403), `NotFound` ("not_found", 404), `Rejected` ("rejected", 422, message = joined `errors`), `RateLimited` ("rate_limited", 429), `InsecureUrl` ("insecure_url"), `HttpError` ("http_error"), `Timeout` ("timeout"), `InvalidResponse` ("invalid_response").
- Produces `IDiscourseClient`:
  - `Task<DiscourseCurrentUser> GetCurrentUserAsync(DiscourseSettings settings, CancellationToken ct)`: `GET /session/current.json`, reads `current_user.username`, `.moderator`, `.admin`.
  - `Task<DiscourseCategory> GetCategoryAsync(DiscourseSettings settings, int categoryId, CancellationToken ct)`: `GET /c/{id}/show.json`, reads `category.id`, `.name`, `.read_restricted`, `.permission` (`CanCreateTopic` when `permission == 1`).
  - `Task<DiscourseTopic?> FindTopicByExternalIdAsync(DiscourseSettings settings, string externalId, CancellationToken ct)`: `GET /t/external_id/{escaped}.json`; on 301/302 follows the `Location` once (same host only, else `InvalidResponse`); null on 404.
  - `Task<DiscourseTopic> GetTopicAsync(DiscourseSettings settings, int topicId, CancellationToken ct)`: `GET /t/{id}.json`, reads `id`, `title`, `deleted_at`, `post_stream.posts[0].id`.
  - `Task<DiscoursePost> GetPostAsync(DiscourseSettings settings, int postId, CancellationToken ct)`: `GET /posts/{id}.json`, reads `id`, `raw`, `deleted_at`.
  - `Task<DiscourseTopic> CreateTopicAsync(DiscourseSettings settings, string title, string raw, int categoryId, string externalId, CancellationToken ct)`: `POST /posts.json` with `title`, `raw`, `category`, `external_id`; reads `id` (post) and `topic_id`.
  - `Task EditPostAsync(DiscourseSettings settings, int postId, string raw, string editReason, CancellationToken ct)`: `PUT /posts/{id}.json` with `{"post":{"raw":..,"edit_reason":..}}`.
  - `Task UpdateTopicTitleAsync(DiscourseSettings settings, int topicId, string title, CancellationToken ct)`: `PUT /t/-/{id}.json` with `title`.
- Every request sends `Api-Key`, `Api-Username`, `Accept: application/json`; refuses non-secure base URLs before sending (`InsecureUrl`); timeout 30 s. Logs never contain the key or a full body (excerpt at most 500 characters, like `HttpOpenSlidesClient`).

- [ ] **Step 1: Write the recording handler** (queue of canned responses, records method, URL, headers and body), modelled on `RecordingOpenSlidesHandler`.
- [ ] **Step 2: Write failing tests** in `HttpDiscourseClientTests`:
  - `Sends_api_key_and_username_headers`.
  - `Create_topic_posts_title_raw_category_and_external_id` (asserts JSON body fields and parsed `TopicId`/`FirstPostId`).
  - `Edit_post_wraps_raw_and_edit_reason_in_post_object`.
  - `Find_by_external_id_follows_one_redirect` and `Find_by_external_id_returns_null_on_404`, `Find_by_external_id_refuses_foreign_redirect_host`.
  - `Topic_deleted_at_is_read` (`Deleted == true`).
  - `Status_codes_are_translated` with arguments 401 → `unauthorized`, 403 → `unauthorized`, 404 → `not_found`, 422 → `rejected` (message contains the `errors` entries), 429 → `rate_limited` with `RetryAfter` from the header, 500 → `http_error`.
  - `Insecure_base_url_sends_nothing`.
  - `Key_never_appears_in_logs` (use `CapturingLogger`).
- [ ] **Step 3: Run, expect compile failure.**
- [ ] **Step 4: Implement** the client with `System.Text.Json` (`JsonDocument` reads, `JsonSerializer` for bodies) and register it in `Program.cs`.
- [ ] **Step 5: Run** `HttpDiscourseClientTests`; expect PASS.

### Task 4: Topic title and first post

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Forum/AssemblyMotionForumInput.cs` (record), `AssemblyMotionForumPost.cs`
- Modify: `Quartermaster.Server/AssemblyMotions/Wiki/AssemblyMotionWikiPage.cs` (page URL)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionForumPostTests.cs`

**Interfaces:**
- Produces `record AssemblyMotionForumInput(AssemblyMotion Motion, AssemblyMotionType Type, string EventName, IReadOnlyList<string> SubmitterNames, string StatusLabel, string PortalUrl, string? WikiUrl)`.
- Produces `static class AssemblyMotionForumPost`:
  - `const int MaxTitleLength = 255`.
  - `static string Title(AssemblyMotionForumInput input)`.
  - `static string Raw(AssemblyMotionForumInput input, int maxPostLength, bool forceNotices = false)`: normalized like `WikiPageText.Normalize`.
  - `static string Hash(string title, string raw)`: SHA-256 hex of `title + "\n" + raw`.
  - `static string DefuseMentions(string text)`.
- Produces `static string AssemblyMotionWikiPage.Url(string apiUrl, string title)`: `apiUrl` with a trailing `api.php` replaced by `index.php?title=` plus the title with spaces as `_`, escaped with `Uri.EscapeDataString` but keeping `/` and `:`.

Fixed copy (German, exact):
- Header line: `{type.Name} {DisplayNumber}: {Title}`; then `Antragsteller: {names joined with ", "}`; `Status: {StatusLabel}`; `Antragsgruppe: {Section}` only when set.
- Revision line while `IsInRevision`: `**Dieser Antrag wird überarbeitet und sucht erneut Mitantragsteller.**`
- `### Zusammenfassung`, then the summary or `Für diesen Antrag wurde keine Zusammenfassung angegeben.`
- Blocks `[details="Antragstext"]` and `[details="Begründung"]`, each closed with `[/details]` on its own line.
- Notices: `Der Antragstext ist zu lang für das Forum und steht vollständig im Antragsportal und im Wiki.` / `Die Begründung ist zu lang für das Forum und steht vollständig im Antragsportal und im Wiki.`; without `WikiUrl` both end in `... im Antragsportal.`
- Links: `Antrag im Antragsportal: {PortalUrl}`, `Antrag im Wiki: {WikiUrl}` only when set.
- Footer: `_Dieser Beitrag wird automatisch aus dem Antragsportal aktualisiert, Änderungen hier werden überschrieben._`
- Mentions: `DefuseMentions` puts a backslash before every `@` that starts a word (`(?<![\w\\])@(?=[\w.-])`), applied to title, section, summary, text, reason and names. Task 10 confirms this form renders as plain `@name` on the forum without a notification.
- A line inside text or reason that consists of `[/details]` (any case, optional whitespace) is escaped as `\[/details]` so the block cannot be closed early.
- Length rule: build with full text and reason; if longer than `maxPostLength`, replace the text with its notice; if still longer, replace the reason too; `forceNotices` replaces both from the start.
- Title: when longer than 255, cut the motion title so that the whole title including ` (...)` suffix and `...` fits; the number and event name always stay.

- [ ] **Step 1: Write failing tests** in `AssemblyMotionForumPostTests` (build inputs with plain `new AssemblyMotion { ... }`, no DB):
  - `Full_post_contains_all_parts_in_order` (asserts header, names, status, section, summary heading, both details blocks, both links, footer appear in this order).
  - `Missing_summary_shows_the_notice`, `Missing_section_has_no_line`, `Revision_line_only_while_in_revision`, `Wiki_line_only_with_wiki_url`.
  - `Too_long_text_is_replaced_by_notice` (text 40,000 characters, `maxPostLength` 32000 → contains the text notice, reason still full, result length ≤ 32000).
  - `Too_long_text_and_reason_are_both_replaced`, `Force_notices_replaces_both`, `Notices_without_wiki_mention_portal_only`.
  - `Mentions_are_defused_everywhere` (`@anna` in title, summary, text and reason → `\@anna`; `mail@example.org` unchanged).
  - `Details_closing_tag_in_text_cannot_end_the_block`.
  - `Title_is_cut_to_255_keeping_number_and_event` (title 500 characters → length 255, starts with `PA001: `, ends with `... (Bundesparteitag 2026.2)`).
  - `Hash_changes_with_title_or_raw`.
  - In `AssemblyMotionWikiRendererTests` or a new small test: `Wiki_page_url_is_built_from_api_url` (`https://wiki.piratenpartei.de/api.php`, `Antrag:BPT 2026.2/PA001` → `https://wiki.piratenpartei.de/index.php?title=Antrag:BPT_2026.2/PA001`).
- [ ] **Step 2: Run, expect compile failure.**
- [ ] **Step 3: Implement** `AssemblyMotionForumInput`, `AssemblyMotionForumPost` and `AssemblyMotionWikiPage.Url`.
- [ ] **Step 4: Run** `AssemblyMotionForumPostTests`; expect PASS.

### Task 5: Forum sync and hosted service

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Forum/AssemblyMotionForumSync.cs`, `AssemblyMotionForumSyncHostedService.cs`
- Modify: `Quartermaster.Server/Program.cs` (scoped sync, hosted service next to the wiki one)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionForumSyncTests.cs`, `Fakes/FakeDiscourseClient.cs`

**Interfaces:**
- Consumes: Task 1 repository methods, Task 2 `DiscourseSettings`, Task 3 `IDiscourseClient` and records, Task 4 `AssemblyMotionForumPost`, `AssemblyMotionWikiPage.Url`, `AssemblyMotionSubmitterNames.ForMotions`, `AssemblyMotionWikiSync.BackoffFor`.
- Produces `AssemblyMotionForumSync(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals, EventRepository events, DbContext db, IDiscourseClient forum, OptionRepository options, AuditLogRepository auditLog, ILogger<AssemblyMotionForumSync> logger)`:
  - `const int DueBatchSize = 10`.
  - `Task<int> SyncDueAsync(DateTime nowUtc, CancellationToken ct)`: returns created plus edited plus unchanged-confirmed count.
  - `internal static void ResetUnconfiguredWarning()`.
  - Audit actions: `ForumSyncFailed` (first failure), `ForumContentDiffered` (post raw differs from what we last sent), `ForumTopicDeleted`.
  - Status label: reuse the German status text the portal shows (`AssemblyMotionDisplay` lives in Blazor; add a small server-side `AssemblyMotionStatusText.German(AssemblyMotionStatus, AssemblyMotionResult)` in `Forum/` with the same wording as `de.json`).
  - Edit reasons: `Antragsportal: Revision {n} eingereicht` when `ForumSyncedRevision != Revision`, else `Antragsportal: Status aktualisiert (Revision {n})`.
  - Error texts stored on the motion: German fixed texts for configuration problems (`Die öffentliche Basis-URL (system.public_base_url) ist nicht gesetzt.`, `Für das Portal ist keine Forum-Kategorie eingestellt.`, `Das Thema wurde im Forum gelöscht.`), Discourse's own `errors` text for `rejected`, generic texts for HTTP failures; never a response body.
- Produces `FakeDiscourseClient : IDiscourseClient` with static state like `FakeWikiBotClient` (`[NotInParallel("FakeDiscourseClient")]`, `Reset()`): topics by id with title, first post raw, deleted flag, external id; counters per method; `ThrowOn` by method name; `RejectRawLongerThan` (int?, makes create/edit throw `Rejected`); `CurrentUser`.

Run flow (per spec "Sync"): unconfigured → warn once per hour, return 0; `GetCurrentUserAsync` → on any exception log and return 0 without touching motions; then each due motion: render, `FindTopicByExternalIdAsync` when no topic stored, create or take over, compare hash, read topic and post, edit, update title, complete. Failures: `Unauthorized` and missing category count as permanent (`MaxBackoff` 6 h); `RateLimited` ends the run without recording; `NotFound` or deleted flags → `MarkForumDeleted` plus audit; `Rejected` while full text was in → one immediate retry with `forceNotices: true`.

- [ ] **Step 1: Write `FakeDiscourseClient`.**
- [ ] **Step 2: Write failing tests** in `AssemblyMotionForumSyncTests` (setup like `AssemblyMotionWikiSyncTests`: `RepositoryTestBase`, options `discourse.*` set, `system.public_base_url` = `https://qm.test`, portal with `ForumMirrorEnabled` and `ForumCategoryId = 7`):
  - `Creates_topic_with_external_id_and_stores_ids` (external id `qm-motion-{id}`, category 7, state Synced, ids stored).
  - `Takes_over_topic_found_by_external_id` (fake already holds the topic, no create call, ids stored).
  - `Unchanged_hash_sends_nothing` (second run: no get, edit or title calls).
  - `Changed_motion_edits_post_with_revision_reason`, `Changed_title_updates_topic_title`.
  - `Moderator_edit_is_overwritten_and_audited` (fake raw changed between runs → `ForumContentDiffered` and edit).
  - `Rejected_long_post_is_retried_with_notices` (`RejectRawLongerThan = 2000`, text 5,000 characters → second call succeeds with the notice, state Synced).
  - `Rejected_short_post_records_failure` (rejects everything → Failed, error contains Discourse's message, `ForumNextAttemptAt = now + 1 min`).
  - `Unauthorized_is_permanent` (create throws `Unauthorized` → `ForumNextAttemptAt = now + 6 h`).
  - `Rate_limit_ends_run_without_failure` (two due motions, first throws `RateLimited` → no failure recorded on either, second not attempted).
  - `Failed_session_check_touches_no_motion`.
  - `Topic_with_deleted_at_marks_the_motion_deleted` and `Missing_topic_marks_the_motion_deleted` (no edit call, state Deleted, audit `ForumTopicDeleted`).
  - `Disabled_portal_is_not_synced`, `Unconfigured_settings_do_nothing`, `Missing_category_is_permanent_failure`.
  - `Post_contains_submitter_names_and_wiki_link_when_wiki_mirrors`.
  - `Newer_request_during_sync_keeps_motion_pending` (motion marked pending again inside the fake's create callback → stays Pending).
- [ ] **Step 3: Run, expect compile failure.**
- [ ] **Step 4: Implement** `AssemblyMotionStatusText`, `AssemblyMotionForumSync`, the hosted service (copy of `AssemblyMotionWikiSyncHostedService` with forum names), and the registrations.
- [ ] **Step 5: Run** `AssemblyMotionForumSyncTests`; expect PASS.

### Task 6: Connection check, settings check and portal settings

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Forum/AssemblyMotionForumConnectionCheck.cs`, `ForumConnectionCheckResult.cs`
- Create: `Quartermaster.Server/Settings/Checks/ForumSettingsCheck.cs`
- Create: `Quartermaster.Server/AssemblyMotions/Endpoints/Portal/AssemblyMotionForumValidateEndpoint.cs`
- Create: `Quartermaster.Server/AssemblyMotions/Forum/AssemblyMotionForumResync.cs`
- Modify: `Quartermaster.Api/AssemblyMotions/AssemblyMotionPortalRequests.cs` (`ForumMirrorEnabled`, `ForumCategoryId` on the update request)
- Modify: `AssemblyMotionPortalUpdateEndpoint.cs`, `AssemblyMotionPortalUpdateRequestValidator.cs`, `Program.cs`, `I18nKey.cs`, `de.json`, `en.json`
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionForumConnectionCheckTests.cs`, `Integration/AssemblyMotions/AssemblyMotionForumEndpointTests.cs`, `AssemblyMotionPortalValidatorTests.cs`

**Interfaces:**
- Produces `record ForumConnectionCheckResult(bool Ok, bool Configured, string Message, IReadOnlyList<string> Warnings)`.
- Produces `AssemblyMotionForumConnectionCheck(IDiscourseClient forum, OptionRepository options, ILogger<...>)` with `Task<ForumConnectionCheckResult> CheckAsync(int? categoryId, CancellationToken ct)`: current user (fails when unauthorized; warning `Das Konto {name} ist kein Moderator; Discourse begrenzt dann Themen, Bearbeitungen und Links.` when not staff); with a category id: category exists (fail `Die Forum-Kategorie {id} existiert nicht.`), `CanCreateTopic` (fail `Das Konto darf in dieser Kategorie keine Themen anlegen.`), warning `Die Kategorie ist öffentlich lesbar; die Beiträge enthalten die Namen der Antragsteller.` when not `ReadRestricted`. Only fixed texts reach the message (like `AssemblyMotionWikiConnectionCheck.Reason`).
- Produces `ForumSettingsCheck : ISettingsCheck` (`Kind = SettingsCheckKind.Forum`, checks without category, warnings appended to the OK message).
- Produces endpoint `POST /api/assemblymotions/portals/{PortalId}/forum/validate` (body `AssemblyMotionForumValidateRequest { Guid PortalId; int? CategoryId }`, declared in `AssemblyMotionPortalRequests.cs`) returning `AssemblyMotionConnectionCheckDTO` plus a new `List<string> Warnings` property on that DTO; guarded like `AssemblyMotionWikiValidateEndpoint`.
- Produces `AssemblyMotionForumResync(AssemblyMotionRepository, AssemblyMotionPortalRepository, AuditLogRepository)` with `AssemblyMotionOutcome QueueOne(Guid motionId, DateTime nowUtc)` (404 unknown, 409 `ForumMirrorDisabled`, 409 `ForumMotionNotNumbered`, 409 `ForumTopicDeleted`), `int QueueAll(Guid portalId, DateTime nowUtc)` (audit `ForumResyncRequested` with count), `int QueueNumbered(Guid portalId, DateTime nowUtc)`, `void ResetPending(Guid portalId)`.
- Portal update: validator requires `ForumCategoryId > 0` while `ForumMirrorEnabled` (`Error.AssemblyMotion.Portal.ForumCategoryRequired`); endpoint runs `CheckAsync(req.ForumCategoryId)` before writing when mirroring gets enabled or the category changes while enabled, and refuses with 422 on `!Configured` (`Error.AssemblyMotion.ForumNotConfigured`) or `!Ok` (`Error.AssemblyMotion.ForumCheckFailed` with `reason`); warnings do not block. After saving: enable → `QueueNumbered`; disable → `ResetPending`.

- [ ] **Step 1: Write failing tests:**
  - `AssemblyMotionForumConnectionCheckTests`: `Unconfigured_is_reported`, `Unauthorized_fails_with_fixed_text` (message does not contain the fake's exception text), `Non_moderator_is_a_warning`, `Missing_category_fails`, `Category_without_create_permission_fails`, `Public_category_is_a_warning`.
  - `AssemblyMotionPortalValidatorTests`: `Forum_mirror_requires_category`.
  - `AssemblyMotionForumEndpointTests` (integration, fake client registered like `AssemblyMotionWikiEndpointTests`): `Enabling_forum_mirror_queues_numbered_motions`, `Enabling_with_failing_check_is_refused_and_nothing_saved`, `Disabling_resets_pending_motions`, `Validate_endpoint_requires_portal_edit_permission`, `Validate_endpoint_returns_warnings`.
- [ ] **Step 2: Run, expect compile failure.**
- [ ] **Step 3: Implement** the check, the settings check (register as `ISettingsCheck`), the resync service, the validate endpoint and the portal update changes, plus the error and UI i18n keys.
- [ ] **Step 4: Run** the three test classes and `AssemblyMotionWikiEndpointTests`; expect PASS.

### Task 7: Commission resync endpoints, dashboard and detail data

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Endpoints/Commission/AssemblyMotionForumResyncEndpoint.cs`, `AssemblyMotionForumResyncAllEndpoint.cs`
- Modify: `Quartermaster.Api/AssemblyMotions/AssemblyMotionCommissionDTOs.cs`, `AssemblyMotionCommissionRequests.cs`, `AssemblyMotionConnectionCheckDTO.cs`
- Modify: `AssemblyMotionDashboardEndpoint.cs`, `AssemblyMotionDashboardRequestValidator.cs`, `AssemblyMotionDetailBuilder.cs`
- Test: `Integration/AssemblyMotions/AssemblyMotionForumEndpointTests.cs`

**Interfaces:**
- Consumes: Task 6 `AssemblyMotionForumResync`.
- Produces routes `POST /api/assemblymotions/motions/{Id}/forum/resync` (like `AssemblyMotionWikiResyncEndpoint`) and `POST /api/assemblymotions/portals/{PortalId}/forum/resync` returning `AssemblyMotionForumResyncAllDTO { int Queued }` (guarded like `AssemblyMotionWikiResyncAllEndpoint`, 409 when the portal does not mirror to the forum).
- Produces on `AssemblyMotionDashboardItemDTO`: `ForumSyncState ForumSyncState`; on `AssemblyMotionDashboardResponse`: `int FailedForumSyncCount`, `bool ForumMirrorEnabled`; on `AssemblyMotionDashboardRequest`: `ForumSyncState? ForumSyncState`, `bool? ForumFailed` (Failed only).
- Produces on `AssemblyMotionCommissionInfoDTO`: `bool ForumMirrorEnabled`, `ForumSyncState ForumSyncState`, `int? ForumSyncedRevision`, `DateTimeOffset? ForumLastSyncedAt`, `string? ForumLastError`, `string? ForumTopicUrl`.

- [ ] **Step 1: Write failing tests** in `AssemblyMotionForumEndpointTests`: `Resync_one_requeues_failed_motion`, `Resync_one_refuses_deleted_topic` (409), `Resync_one_requires_commission`, `Resync_all_counts_numbered_motions_except_deleted`, `Dashboard_filters_by_forum_state_and_counts_failures`, `Commission_detail_carries_forum_state_and_topic_url`.
- [ ] **Step 2: Run, expect compile failure.**
- [ ] **Step 3: Implement** the endpoints, DTO fields, dashboard filter and counts, and `BuildCommissionInfo` (topic URL via `DiscourseSettings.TopicUrl` only when a topic id is stored and the state is not Deleted). `QueueAll` must skip Deleted motions (it relies on `MarkForumPending`).
- [ ] **Step 4: Run** `AssemblyMotionForumEndpointTests`; expect PASS.

### Task 8: Forum link on the public motion page (server)

**Files:**
- Modify: `Quartermaster.Api/AssemblyMotions/AssemblyMotionMemberDTOs.cs` (`AssemblyMotionDetailDTO.ForumTopicUrl`)
- Modify: `Quartermaster.Server/AssemblyMotions/AssemblyMotionDetailBuilder.cs`
- Test: `Integration/AssemblyMotions/AssemblyMotionForumEndpointTests.cs`

**Interfaces:**
- Produces `string? ForumTopicUrl` on `AssemblyMotionDetailDTO`, set in `Build` only when the viewer is logged in (`viewerMember != null || viewer.IsCommission`), the motion has a `ForumTopicId`, and the state is not Deleted.

- [ ] **Step 1: Write failing tests:** `Forum_link_is_hidden_from_anonymous_viewers`, `Forum_link_is_shown_to_logged_in_members`, `Forum_link_is_hidden_for_deleted_topics`.
- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement** (inject `DiscourseSettings` into `AssemblyMotionDetailBuilder`).
- [ ] **Step 4: Run** `AssemblyMotionForumEndpointTests`; expect PASS.

### Task 9: Blazor UI

**Files:**
- Modify: `Quartermaster.Blazor/Components/AssemblyMotions/AssemblyMotionPortalSettings.razor(.cs)`
- Modify: `Quartermaster.Blazor/Pages/Administration/AssemblyMotions/AssemblyMotionCommissionDetail.razor(.cs)`, `AssemblyMotionDashboard.razor(.cs)`
- Modify: `Quartermaster.Blazor/Pages/AssemblyMotions/AssemblyMotionDetail.razor(.cs)`
- Modify: the Blazor display helper that holds `WikiSyncBadgeClass` / `WikiSyncLabel` (`AssemblyMotionDisplay`), `I18nKey.cs`, `de.json`, `en.json`

**Interfaces:**
- Consumes: Tasks 6 to 8 DTOs and routes.
- Produces helpers `AssemblyMotionDisplay.ForumSyncBadgeClass(ForumSyncState)` and `ForumSyncLabel(I18nService, ForumSyncState)` (labels Ausstehend, Synchronisiert, Fehlgeschlagen, Im Forum gelöscht, Nicht gespiegelt).

- [ ] **Step 1: Portal settings:** "Forum" block below the wiki block with checkbox `forumMirrorEnabled`, number input `forumCategoryId` (readonly and disabled while mirroring is off or `!CanEdit`), "Verbindung prüfen" calling the validate route and showing message plus warnings; fields sent with the portal update.
- [ ] **Step 2: Commission detail:** "Forum" card mirroring the wiki card (state badge, synced revision, last sync, topic link with `target="_blank" rel="noopener"`, last error, "Neu synchronisieren" hidden for Deleted).
- [ ] **Step 3: Dashboard:** forum column, state filter, failed notice with "Fehlgeschlagene anzeigen", "Alle neu synchronisieren", all only while `ForumMirrorEnabled`.
- [ ] **Step 4: Public detail page:** "Diskussion im Forum" link (`bi-chat-square-text`, new tab) when `ForumTopicUrl != null`.
- [ ] **Step 5: Build the full solution, restart the dev server** with the restart script (own Bash call), and check with curl that the dashboard, commission detail and portal settings API answers carry the new fields (no Chrome).

### Task 10: Docs and forum verification

**Files:**
- Modify: `Quartermaster.Documentation/Configuration.md`, `Translations.md`
- Create: `Quartermaster.Server/Changelog/V6.1.md`

- [ ] **Step 1: `Configuration.md`:** a "Discourse forum" section: the four options, creating a dedicated bot account with moderator rights, creating an API key for that user (scopes: topics write and update, posts edit, or a single-user key without scopes), matching `discourse.max_post_length` to the forum's `max_post_length`, the per-portal fields, the check, and what happens on deletion, mirror disable and enable.
- [ ] **Step 2: `Translations.md`:** Forum-Spiegelung = forum mirroring, Forum-Kategorie = forum category, Im Forum gelöscht = deleted in the forum.
- [ ] **Step 3: `Changelog/V6.1.md`:** user-visible summary in the style of `V6.0.md`.
- [ ] **Step 4: Hand over the forum check to the user** (not run by the implementer): in a test category, post a topic containing `\@username` and confirm it renders as `@username` without notifying the user; if not, change `DefuseMentions` and its test before release.
- [ ] **Step 5: Sweep the whole diff against `CLAUDE.md`** and for em or en dashes (`grep -nP '[\x{2013}\x{2014}]'` on changed files).
