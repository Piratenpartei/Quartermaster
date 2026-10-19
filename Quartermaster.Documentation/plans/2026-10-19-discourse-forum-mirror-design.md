# Discourse forum mirror for assembly motions (V6.1)

## Goal

When a motion in the motion portal is submitted, a topic is created in a configured Discourse category of the party forum. The first post carries the motion's summary, key facts, submitter names and the full text (collapsed), and it is kept up to date on every later change, the same way the wiki mirror keeps its page up to date. Replies in the forum are never touched. The aim is to move the discussion of a motion into the forum, where members already are.

Forum and portal share the same user group via SSO, so submitter names may appear in the forum. The wiki is public and not every reader has forum access, so the wiki gets no link to the forum.

## Decisions

- Topic first post is kept up to date (option B): revisions, status changes, results, inquiries, competition and co-submitter changes all update it. Topics are never closed or archived by Quartermaster.
- Post layout is fixed in code (not an editable template), German.
- Full text in a collapsed `[details]` block; a part that does not fit the forum's post length is replaced by a notice, never cut short.
- Forum link shown only in the portal (to logged-in users), not in the wiki.
- Separate sync next to the wiki sync (no generic mirror abstraction), sharing the trigger points.
- Ships in V6.1 with migration `M006_1` (`[Migration(009)]`).

## Discourse constraints

Defaults of a Discourse site that shape the design (`config/site_settings.yml`):

- `max_post_length` 32,000 (max 150,000), counted on the raw text after whitespace collapsing, so our own character count is a safe upper bound. Not readable through the API with a non-admin key.
- `max_topic_title_length` 255, `min_topic_title_length` 15, `allow_duplicate_topic_titles` false (site wide).
- Ordinary accounts: 20 topics per day, 30 edits per day, own posts editable for 24 hours only (`post_edit_time_limit`), new users at most 2 links per post. Staff accounts are exempt, so the bot account must be a moderator.
- API keys created by an admin allow about 60 requests per minute; 429 answers carry `Retry-After`.
- A topic can be created with an `external_id` and looked up via `/t/external_id/{id}.json`. An external id is never accepted twice, not even after the topic was deleted.

Quartermaster side: motion text and reason are up to 100,000 characters each (`MotionMarkdown.MaxLength`), the summary is optional plain text up to 2,000 characters, the title up to 500 characters.

## Settings

Global options (in `OptionRepository`, `SettingsAssistantCatalog`, a "Forum (Discourse)" assistant on the settings card hub):

- `discourse.base_url`, for example `https://forum.piratenpartei.de`. Must be https, http only on localhost, 127.0.0.1 and [::1] (same rule as `WikiBotSettings.IsSecureUrl`).
- `discourse.api_username`, the bot account (a moderator).
- `discourse.api_key`, a key bound to that user; with granular scopes it needs topics write/update and posts edit. Stored and masked like `wiki.bot_password`, never logged (`DiscourseSettings.ToString` leaves it out).
- `discourse.max_post_length`, default 32,000, set to the forum's value.

Per portal (`AssemblyMotionPortal`, portal settings UI below the wiki block):

- `ForumMirrorEnabled` (bool).
- `ForumCategoryId` (int?), required while mirroring is on.

Connection check (settings check and a "Verbindung prüfen" button in the portal settings): key and account valid (`/session/current.json`), account is moderator or admin (warning otherwise), category exists and the bot can create topics there, warning when the category is publicly readable (`read_restricted` false) because the posts carry member names.

## Topic title

`{Number}: {Title} ({Event name})`, for example "PA001: Titel des Antrags (Bundesparteitag 2026.2)". The event name keeps titles unique across assemblies, since motion numbers start again per assembly. A title longer than 255 characters is cut and ends with "...". The title is updated when it changes (revision with a new title).

## First post

Built by `AssemblyMotionForumPost` from the motion, its type, portal and event, the submitter names, the portal URL and (when the portal mirrors to the wiki) the wiki URL:

```
Programmantrag PA001: Titel des Antrags
Antragsteller: Anna Pirat, Bernd Beispiel
Status: Eingereicht
Antragsgruppe: Umwelt

**Dieser Antrag wird überarbeitet und sucht erneut Mitantragsteller.**

### Zusammenfassung
<summary or "Für diesen Antrag wurde keine Zusammenfassung angegeben.">

[details="Antragstext"]
<motion text Markdown or the length notice>
[/details]

[details="Begründung"]
<reason Markdown or the length notice>
[/details]

Antrag im Antragsportal: <portal URL>
Antrag im Wiki: <wiki URL>

_Dieser Beitrag wird automatisch aus dem Antragsportal aktualisiert, Änderungen hier werden überschrieben._
```

- "Antragsgruppe" only when `Section` is set; the revision line only while `IsInRevision`; the wiki line only when the portal mirrors to the wiki.
- Status labels reuse the existing German status texts.
- Submitter names via `AssemblyMotionSubmitterNames` (author first, then by join time; former members as `AssemblyMotionDetailBuilder.DisplayName` renders them).
- Length rule: header, summary and links always go in. The motion text goes in when the post stays within `discourse.max_post_length`, then the reason when it still fits. A part that does not fit is replaced by "Der Antragstext ist zu lang für das Forum und steht vollständig im Antragsportal und im Wiki." or "Die Begründung ist zu lang für das Forum und steht vollständig im Antragsportal und im Wiki."; without wiki mirroring the notices end with "im Antragsportal.". The links follow at the end of the post as usual.
- Mentions: an `@name` in user text (title, summary, text, reason, section) must not notify forum users. The escaping is verified against the real forum before release; whichever form is chosen must not change the rendered text.
- Text is normalized (NFC, LF, no trailing whitespace) before hashing, like `WikiPageText`.

## Data model (M006_1)

`AssemblyMotions`: `ForumTopicId` (int?), `ForumPostId` (int?), `ForumSyncState` (new enum `ForumSyncState`: NotMirrored, Pending, Synced, Failed, Deleted), `ForumSyncedRevision` (int?), `ForumSyncedHash` (string?), `ForumLastSyncedAt`, `ForumLastError` (string?, same max length as `WikiLastError`), `ForumFailureCount` (int), `ForumNextAttemptAt`, `ForumSyncRequestedAt`.

`AssemblyMotionPortals`: `ForumMirrorEnabled` (bool, default false), `ForumCategoryId` (int?).

File `Quartermaster.Data/Migrations/M006_1.cs`, `class M006_1`, `[Migration(009)]`.

## Queuing

- `AssemblyMotionRepository.MarkWikiPending` becomes `MarkMirrorsPending`: two conditional updates in one call that set the wiki fields to pending when the portal mirrors to the wiki and the forum fields to pending when the portal mirrors to the forum. All existing call sites switch to it (submit, revision, check, inquiries, competition, results, and the wiki reconcile, since a wiki title change changes the wiki link in the post), except `AssemblyMotionWikiResync`, which stays wiki-only. `MarkWikiPendingAsNeverSynced` (wiki prefix change) also queues the forum, for the same reason.
- New `MarkForumPending(motionId, nowUtc)` for forum-only triggers: `Join` and `Leave` in `AssemblyMotionService` when the motion is numbered (`WasSubmitted`), since the names changed.
- Motions in state Deleted are never queued again.
- Switching forum mirroring on queues every numbered motion of the portal (same as the wiki, which runs `QueueNumberedAsNeverSynced` on enable), and a numbered motion in state NotMirrored counts as due. "Alle neu synchronisieren" queues every numbered motion of the portal that is not Deleted.

## Sync

`AssemblyMotionForumSync` (scoped) plus `AssemblyMotionForumSyncHostedService` (every minute, batch of 10 due motions). A motion is due when it is numbered, its portal mirrors to the forum, and it is Pending, or Failed with `ForumNextAttemptAt` in the past. Like the wiki sync it never holds a transaction or row lock across an HTTP call; it reads, talks to Discourse, then records the outcome with a conditional update keyed on `ForumSyncRequestedAt`.

Each run starts with `GET /session/current.json`. A failure there (401, 403, wrong base URL) is recorded on no motion, logged, and ends the run; this way a 404 later in the run means the topic is really gone and not that the forum is misconfigured.

Per motion:

1. Render title and post; compute the hash.
2. No `ForumTopicId` stored: look up `/t/external_id/qm-motion-{motionId}.json`. Found: take over its topic and first post ids and continue with step 4. Not found: create via `POST /posts.json` with `title`, `raw`, `category` and `external_id`; store topic id and post id.
3. Topic stored and hash equals `ForumSyncedHash`: nothing is sent, the motion is marked Synced.
4. Otherwise: read the topic (`GET /t/{topicId}.json`); a set `deleted_at` means the topic was deleted (moderators can still read deleted topics, so a deletion does not always answer 404). Then read the first post (`GET /posts/{id}.json`, also checked for `deleted_at`); when its raw text differs from what we last sent, write an audit entry `ForumContentDiffered`. Edit the post (`PUT /posts/{id}.json` with `raw` and an edit reason: "Antragsportal: Revision N eingereicht" or "Antragsportal: Status aktualisiert (Revision N)"). When the title changed, update the topic title (`PUT /t/-/{topicId}.json` with `title`). The category is never changed after creation; moderators may move topics, and a portal category change only affects new topics.
5. Record Synced with revision, hash and time.

Errors:

- Same backoff as the wiki (`BackoffFor`: 1, 2, 4 minutes and so on, capped at 6 hours); first failure writes an audit entry `ForumSyncFailed`.
- Permanent (6 hours right away): 401 or 403 on a motion request, missing category, base URL (`system.public_base_url`) not configured.
- 422 on create or edit while the post contained full text: retry once at once with both parts replaced by notices. A second 422 is recorded as a failure with Discourse's error message.
- 429: stop the run without counting a failure; the motion stays due.
- Topic or post deleted (`deleted_at` set on the topic or first post): state Deleted, error "Das Thema wurde im Forum gelöscht.", no further syncs, not even on resync.
- Topic or post answers 404: permanent failure (6 hours, visible error), not Deleted, because Discourse answers 404 both for a hard-deleted topic and for one the bot can no longer see.
- Mirroring switched off for the portal: nothing is synced; existing topics stay as they are. Pending motions of that portal go back to NotMirrored (like `ResetWikiPendingToNotMirrored`).
- Unconfigured global settings: the run does nothing and logs a warning at most once per hour.

`AssemblyMotionForumResync`: one motion (resets state, failure count and backoff; refused for Deleted) and all motions of a portal.

## UI

- Portal settings (`AssemblyMotionPortalSettings`): "Forum-Spiegelung" checkbox, "Forum-Kategorie" (id) field, "Verbindung prüfen". Fields readonly and disabled while mirroring is off or the user cannot edit.
- Commission detail (`AssemblyMotionCommissionDetail`): "Forum" card next to the wiki card with state badge (Ausstehend, Synchronisiert, Fehlgeschlagen, Im Forum gelöscht), synced revision, last sync, topic link, last error and "Neu synchronisieren" (hidden for Deleted).
- Commission dashboard (`AssemblyMotionDashboard`), only while the portal mirrors to the forum: "Forum" column with the badge, state filter, "N Forum-Abgleiche fehlgeschlagen" with "Fehlgeschlagene anzeigen", "Alle neu synchronisieren".
- Public motion page: "Diskussion im Forum" link (new tab), only for logged-in users and only when a topic exists and the state is not Deleted. The topic URL is `{base_url}/t/{topicId}`.
- All texts as i18n keys (de and en).

## Code layout

Under `Quartermaster.Server/AssemblyMotions/Forum/`, parallel to `Wiki/`:

- `IDiscourseClient`, `HttpDiscourseClient` (HTTP only: headers `Api-Key` and `Api-Username`, JSON bodies, status codes translated into `DiscourseException` with a code).
- `DiscourseException`, `DiscourseSettings` (options, `IsConfigured`, https rule, safe `ToString`).
- `AssemblyMotionForumPost` (title and post text, length rule, mention defusing).
- `AssemblyMotionForumSync`, `AssemblyMotionForumSyncHostedService`, `AssemblyMotionForumResync`.
- `AssemblyMotionForumConnectionCheck` and a `ForumSettingsCheck` under `Settings/Checks/`.
- Endpoints for resync (one, all) under `Endpoints/Commission/`, guarded like the wiki resync endpoints; portal settings endpoints gain the two fields and validate the category id.

## Tests

No test or dev run contacts a real Discourse; `FakeDiscourseClient` replaces `IDiscourseClient`, and `HttpDiscourseClient` is tested against a fake `HttpMessageHandler`.

- `AssemblyMotionForumPost`: layout, missing summary, missing section, revision line, wiki line only with wiki mirroring, names in order, length notice for text only, reason only and both, title cut at 255, defused mentions.
- `AssemblyMotionForumSync`: create with external id; take-over via external id after a lost create; no request on an unchanged hash; edit on a changed hash; title update; audit entry after a moderator edit; 422 retry with the short version; backoff and permanent codes; a failed session check ends the run without touching any motion; 429 stops the run without a failure; 404 leads to Deleted and resync refuses it; mirroring off; unconfigured settings.
- Queuing: the former `MarkWikiPending` call sites queue both targets per portal flags; `Join` and `Leave` on a numbered motion queue the forum only.
- `HttpDiscourseClient`: headers, request bodies, status code translation, `Retry-After`.
- Endpoints: resync one and all with permissions; portal settings with the category validation; forum link only for logged-in users.

The first real test runs against the forum, ideally in a test category, after the bot account is set up as a moderator.

## Docs

- `Configuration.md`: Discourse section with the four options, setting up the bot account as moderator, creating the API key and its scopes, matching `max_post_length`, the per-portal fields.
- `Translations.md`: Forum-Spiegelung = forum mirroring, Forum-Kategorie = forum category.
- `Changelog/V6.1.md`: user-visible summary.
