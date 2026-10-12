# Assembly Motion Portal: Design

## Goal

Members submit motions for a party assembly (Bundesparteitag, Landesparteitag) inside Quartermaster, signed in through the SSO. Other members join as co-submitters with their own login. The motion commission (Antragskommission, AKO) does its formal check in the same place. Every submitted motion is mirrored automatically into the Piratenwiki, so the archive stays findable after the portal is gone.

The portal replaces the wiki forms under `Antrag:Bundesparteitag JJJJ.N/Antragsportal`. The wiki stays the permanent archive, Quartermaster becomes the place where motions are written and processed.

## Terms

| German (UI) | English (code) | Meaning |
|---|---|---|
| Versammlung | assembly | An `Event` with the motion portal enabled |
| Antragsportal | motion portal | The per-event feature described here |
| Antrag | assembly motion | A motion addressed to an assembly. Not the existing board `Motion` |
| Antragsart | motion type | GP, WP, SÄA, SBA, PP, SO or whatever the event configures |
| Antragsteller | author | The member who created the motion |
| Mitantragsteller | co-submitter | A member who joined the motion; counts toward the required number of submitters |
| Antragskommission (AKO) | motion commission | Users holding `assemblymotions_manage` for the event's chapter |
| Formprüfung | formal check | The commission's decision whether a motion is admitted |
| Rückfrage | inquiry | A question from the commission to the submitters with a response deadline |
| Konkurrenz | competition | Two motions that exclude each other |
| Priorisierung | prioritization | The optional upvote phase that determines the order of motions |

Add these to `Quartermaster.Documentation/Translations.md`.

## Scope

In scope: portal configuration per event; motion types with deadlines; drafting, co-submitting, submitting, editing, withdrawing; formal check, inquiries, commission edits; competition links; the optional prioritization phase; recording results after the assembly; public read views; the wiki mirror; pushing motions into OpenSlides; a JSON export; notifications; one new permission; audit logging.

Not in scope: discussion or comments on motions; voting on motions (that happens in OpenSlides); the commission's proposal for block votes; a manual ordering of motions by the commission; marking accepted motions as incorporated into statutes or programme; any change to the existing board `Motion` feature; generation of the wiki overview pages (they fill themselves from the motion pages through Semantic MediaWiki queries).

## Relationship to Existing Code

- The existing `Motion` entity is the board motion (chapter officers vote, free-text author). It is not reused. Assembly motions get their own entities, repositories, endpoints and pages under an `AssemblyMotions` folder in each project.
- An assembly is an `Event`. The portal attaches to an event 1:1. Events without a portal are unaffected.
- A signed-in user is resolved to a member through `Member.UserId`.
- Chapter tree lookups use `ChapterRepository.GetDescendantIds`.
- Text fields follow the board motion pattern: Markdown source is canonical, sanitized HTML is derived through `MarkdownService`.
- Notifications go through the existing trigger, recipient resolver and channel system so that members receive them on whatever channel they configured (email, Telegram).
- Wiki credentials and endpoints are stored as options next to the existing `keycloak.*` options. Wiki page bodies are rendered with Fluid.

## Data Model

All ids are `Guid`. All timestamps are stored in UTC and follow the existing `ToStorage` and `ToDtoUtc` conventions. Deadlines are entered and displayed in Europe/Berlin time.

### AssemblyMotionPortal

One row per event that has the portal enabled.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| EventId | Guid | Unique |
| OpensAt | DateTime | Members can create motions from this time |
| ClosesAt | DateTime | No creation, submission, joining or author edits after this time for any type. Normally the start of the assembly |
| IntroMarkdown, IntroHtml | string | Shown at the top of the public portal page |
| PrioritizationEnabled | bool | |
| PrioritizationStartsAt, PrioritizationEndsAt | DateTime? | Required when enabled; start before end; end not after `ClosesAt` |
| WikiMirrorEnabled | bool | |
| WikiPagePrefix | string? | Required when the mirror is enabled, for example `Antrag:Bundesparteitag 2027.1/Antragsportal` |
| WikiAssemblyLabel | string? | Value for the wiki template parameter `bundesparteitag`, for example `2027.1` |
| OpenSlidesMeetingId | int? | Id of the target meeting in OpenSlides; required for the OpenSlides push |
| CreatedAt | DateTime | |

### AssemblyMotionType

Per portal. Created from a default set when the portal is enabled, then editable.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| PortalId | Guid | |
| Code | string | For example `SÄA`. Unique per portal. Used in the motion number |
| Name | string | For example `Satzungsänderungsantrag` |
| SortOrder | int | |
| MinSubmitters | int | Author included. 1 or more |
| Deadline | DateTime? | Null means the portal's `ClosesAt` |
| CompetingDeadline | DateTime? | Later deadline for motions that compete with an already submitted motion. Null means no extension |
| RequiredMajority | enum | `Simple`, `TwoThirds`. Informational, shown and exported |
| WikiTemplate | string | Fluid template for the wiki page body of this type |
| OpenSlidesCategoryId | int? | Id of the OpenSlides motion category created for this type by the first push |

Default set seeded when a portal is enabled (deadlines empty, to be set by the organizer):

| Code | Name | MinSubmitters | RequiredMajority |
|---|---|---|---|
| GP | Grundsatzprogramm | 5 | TwoThirds |
| WP | Wahlprogramm | 5 | TwoThirds |
| SÄA | Satzungsänderungsantrag | 5 | TwoThirds |
| SBA | Satzungsbeiordnung | 5 | Simple |
| PP | Positionspapier | 1 | Simple |
| SO | Sonstiger Antrag | 1 | Simple |

A type that has motions cannot be deleted, and its `Code` cannot be changed.

### AssemblyMotion

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| PortalId | Guid | |
| TypeId | Guid | Fixed after the first submission |
| Number | int? | Assigned on first submission, see Numbering |
| Title | string | |
| Section | string? | What the motion refers to, for example `Abschnitt B, § 7` or a programme chapter. Wiki parameter `antragsgruppe` |
| TextMarkdown, TextHtml | string | The text the assembly adopts |
| ReasonMarkdown, ReasonHtml | string | Justification |
| Summary | string? | One or two sentences |
| Keywords | string? | Comma separated |
| PriorDiscussion | string? | Free text or link to where the motion was discussed |
| IsCompetingMotion | bool | The author claims the competing deadline; requires at least one competition link |
| Status | enum | See Lifecycle |
| Result | enum | `None`, `Accepted`, `AcceptedModified`, `Rejected`, `NotHandled` |
| Revision | int | Starts at 1, incremented on every content change |
| CheckNote | string? | Public reason given by the commission for the formal check decision |
| CheckedAt, CheckedByUserId | | |
| CreatedByMemberId | Guid | The author |
| CreatedAt, SubmittedAt, WithdrawnAt | DateTime | `SubmittedAt` is the time the current revision reached the required submitters |
| WikiSyncState | enum | `NotMirrored`, `Pending`, `Synced`, `Failed`, `ProtectionFailed` |
| WikiSyncedRevision | int? | Last revision written to the wiki |
| WikiSyncedHash | string? | Hash of the last wikitext written |
| WikiLastSyncedAt, WikiLastError | | |
| OpenSlidesMotionId | int? | Id of the motion in OpenSlides after the first push |
| OpenSlidesPushedRevision | int? | Revision last pushed to OpenSlides |
| OpenSlidesPushedAt, OpenSlidesLastError | | |
| DeletedAt | DateTime? | Soft delete, drafts only |

### AssemblyMotionSubmitter

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| MotionId | Guid | |
| MemberId | Guid | Unique per motion |
| IsAuthor | bool | Exactly one per motion |
| JoinedAt | DateTime | |
| JoinedRevision | int | The revision the member agreed to |

Names are never copied into this table. They are resolved from `Member` when displayed. An anonymized or missing member is shown as "Ehemaliges Mitglied".

### AssemblyMotionRevision

One row per content change, written when the change is saved.

| Field | Type | Notes |
|---|---|---|
| Id, MotionId, Revision | | |
| Title, Section, TextMarkdown, ReasonMarkdown, Summary, Keywords, PriorDiscussion | | Snapshot |
| EditedByUserId, EditedAt | | |
| EditedByCommission | bool | |
| ChangeNote | string? | Required for commission edits |

### AssemblyMotionInquiry

| Field | Type | Notes |
|---|---|---|
| Id, MotionId | | |
| QuestionMarkdown, QuestionHtml | string | |
| AskedByUserId, AskedAt | | |
| ResponseDueAt | DateTime | At least 24 hours after `AskedAt`; the form defaults to 2 days |
| AnswerMarkdown, AnswerHtml | string? | |
| AnsweredByMemberId, AnsweredAt | | |
| ClosedAt, ClosedByUserId | | Set by the commission |

### AssemblyMotionCompetition

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| MotionAId, MotionBId | Guid | Stored with the smaller id first; the pair is unique; both motions belong to the same portal |
| DeclaredByUserId, DeclaredAt | | |
| DeclaredByCommission | bool | |

### AssemblyMotionUpvote

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| MotionId, MemberId | Guid | Unique pair |
| CreatedAt | DateTime | |

The feature ships in version 4: all schema changes go into a new migration `M004` (the next free `[Migration(N)]` number), and the changelog section goes into `V4.md`.

## Eligibility

One service, `AssemblyMotionEligibility`, answers whether the current user may act as a member in a given portal. It is used for creating, joining, upvoting and for seeing names. A user is eligible when all of these hold:

1. A `Member` with `UserId` equal to the user exists.
2. The member has a member number (not floating), is not pending, is not anonymized and has not exited (`IsExited(now)` is false).
3. The member's `ChapterId` is the event's chapter or one of its descendants. A member without a chapter (`ChapterId` null, for example created on first SSO login) is eligible only when the event's chapter is the root chapter (federal assembly).

`HasVotingRights` is not required. For a federal assembly this covers every member; for a state assembly only the members of that state and its sub-chapters.

Eligibility is checked at the time of each action. A later exit does not remove a past co-submission or upvote.

Ineligible signed-in users see the public view plus a short explanation of why they cannot take part.

## Lifecycle

```
Draft -> CollectingCoSubmitters -> Submitted -> Admitted
                                             -> FormallyInsufficient
any state before the assembly -> Withdrawn
CollectingCoSubmitters -> Lapsed (deadline passed without enough submitters)
```

| Status | Meaning | Visible to |
|---|---|---|
| Draft | Author is still writing | Author |
| CollectingCoSubmitters | Released by the author, waiting for the required number of submitters | Everyone |
| Submitted | Required number reached before the deadline; waiting for the formal check | Everyone |
| Admitted | Passed the formal check | Everyone |
| FormallyInsufficient | Rejected in the formal check, with `CheckNote` | Everyone |
| Withdrawn | Withdrawn by the author or the commission | Everyone |
| Lapsed | Deadline passed while collecting | Everyone |

Rules:

- The author releases a draft. If the type's `MinSubmitters` is 1 the motion goes straight to `Submitted`, otherwise to `CollectingCoSubmitters`.
- When the number of submitters reaches `MinSubmitters`, the motion becomes `Submitted` and `SubmittedAt` is set. This transition happens inside the same transaction as the join.
- The effective deadline of a motion is the type's `CompetingDeadline` if `IsCompetingMotion` is true and that deadline is set, otherwise the type's `Deadline`, otherwise the portal's `ClosesAt`. Releasing, joining and submitting are refused after the effective deadline.
- A background job moves motions still in `CollectingCoSubmitters` to `Lapsed` once their effective deadline has passed.
- Only the author can withdraw, at any time before the portal closes. The commission can withdraw on the author's request. A withdrawn motion cannot be reopened; the author creates a new one.
- The commission can move a motion between `Submitted`, `Admitted` and `FormallyInsufficient` freely (a decision can be corrected). `CheckNote` is required for `FormallyInsufficient`.

### Numbering

A motion receives its number at the first transition to `Submitted`: the next free integer for its type within the portal, starting at 1. The displayed number is the type code followed by the number padded to three digits, for example `SÄA001`. A number is never changed and never reused, also not after withdrawal, because the wiki page title is built from it. Number assignment must be safe against concurrent submissions (unique index on portal, type and number; retry on conflict).

### Co-submitters

- An eligible member joins a motion in `CollectingCoSubmitters` from the motion page. The page states the title, the full text and that joining makes the member a co-submitter whose name is visible to signed-in members.
- A co-submitter can leave while the motion is in `CollectingCoSubmitters`. The author cannot leave; the author withdraws instead.
- Once the motion is `Submitted` the list of submitters is frozen. Further members cannot join.
- Motions in `CollectingCoSubmitters` are listed for eligible members on a page "Anträge, die Mitantragsteller suchen" and can be shared by link.

### Editing

By the author:

- `Draft`: free editing.
- `CollectingCoSubmitters`: allowed. Saving a content change increments the revision, removes all co-submitters except the author and notifies each removed member with a link to rejoin.
- `Submitted` before the effective deadline: allowed with the same reset. If the type needs more than one submitter, the motion returns to `CollectingCoSubmitters` and `SubmittedAt` is cleared; it must reach the required number again before the deadline or it lapses. The number is kept. If the type needs only the author, the motion stays `Submitted`.
- An edited motion is the same for everyone: the portal, the public view and the wiki show the current text right away. A numbered motion back in `CollectingCoSubmitters` is shown to everyone as "In Überarbeitung" until it reaches the required submitters again. There is no older "submitted" version that some viewers keep seeing.
- `Admitted`, `FormallyInsufficient`, or any state after the effective deadline: not allowed. The page tells the author to contact the commission and offers the open inquiry, if any, as the channel.

As soon as the author opens the editor for a motion that is not a draft, a clear warning says what saving will do: the motion goes back into revision and is visible as such to everyone, including the wiki; all co-submitters (with their number) are removed and have to join again before the deadline. For types that need only the author, the warning says that the change is public immediately. Saving asks for confirmation with the same text.

By the commission (`assemblymotions_manage`):

- Allowed in every state from `Submitted` on, with a mandatory `ChangeNote`.
- Does not remove co-submitters. All submitters are notified with the change note.

Every saved change writes an `AssemblyMotionRevision`. Signed-in eligible members and the commission can view the revision history with a diff; anonymous visitors see only the current submitted text.

## Formal Check and Inquiries

The commission works from a dashboard per portal: a filterable list of all motions from `CollectingCoSubmitters` on, with type, number, status, open inquiries, submitters, deadline state and wiki sync state.

Formal check: set `Admitted` or `FormallyInsufficient` with `CheckNote`. All submitters are notified of the decision and the note.

Inquiries:

- The commission asks a question on a motion and sets `ResponseDueAt` (minimum 24 hours ahead, default 2 days).
- All submitters are notified. Any submitter can answer in the portal; the commission is notified of the answer.
- The commission closes the inquiry. Several inquiries per motion are possible, one after another or in parallel.
- Inquiries and answers are visible to the submitters and the commission only.
- An inquiry does not change the motion status. Open inquiries are shown as a badge and are reflected in the wiki check icon.

## Competition

- Any submitter of a motion, or the commission, can declare that the motion competes with another motion of the same portal that is `Submitted` or `Admitted`.
- The link is symmetric and shown on both motions, publicly, with the motion numbers and titles.
- The submitters of the other motion are notified.
- A link declared by a submitter can be removed by a submitter of the declaring motion or by the commission. A link declared by the commission can be removed only by the commission.
- `IsCompetingMotion` can be set by the author only while the motion has at least one competition link to a motion that was submitted before it. The commission can clear the flag; if that puts the motion past its effective deadline while it is still collecting, it lapses with the next run of the deadline job.

## Prioritization

Optional per portal. When enabled, eligible members upvote motions during the configured window, and the result defines the order of motions.

- One upvote per member per motion. A member can upvote any number of motions and can take an upvote back during the window.
- Motions in `Submitted` or `Admitted` can be upvoted.
- During the window nobody sees counts, not even the commission. A member sees which motions they upvoted.
- After `PrioritizationEndsAt` the ranking is public: all `Submitted` and `Admitted` motions ordered by upvote count descending, ties broken by earlier `SubmittedAt`, then by number. The count is shown per motion. Who upvoted what is never shown to anyone and is not part of any export.
- The ranking is computed on read. A motion that becomes `FormallyInsufficient` or `Withdrawn` afterwards drops out.
- The ranking page offers two views: one overall list, and the same order grouped by motion type.

## Results

After the assembly the commission records a `Result` per motion in a table view (bulk editing): `Accepted`, `AcceptedModified`, `Rejected`, `NotHandled`. Results are public and are written to the wiki. The result can be set once the event date has been reached.

## Visibility and Privacy

| Data | Anonymous | Signed-in eligible member | Submitter of the motion | Commission |
|---|---|---|---|---|
| Portal page, types, deadlines | yes | yes | yes | yes |
| Every released motion, whatever its status (also still collecting, withdrawn or lapsed): number if it has one, title, text, reason, status, check note, result, competition | yes | yes | yes | yes |
| Numbered motions in revision ("In Überarbeitung"), current text | yes | yes | yes | yes |
| Drafts | no | no | author only | no |
| Names of author and co-submitters | no | yes | yes | yes |
| Member numbers of submitters | no | no | no | yes |
| Revision history | no | yes | yes | yes |
| Inquiries | no | no | yes | yes |
| Upvote counts | after the window | after the window | after the window | after the window |

Motion texts and their status stay public permanently, also after the event is completed or archived.

Enforcement: anonymous endpoints return dedicated public DTOs that have no name or member fields at all. Do not reuse the member DTOs with fields blanked. Tests assert that the anonymous responses never contain a submitter name.

## Wiki Mirror

The requirement: every motion that was ever `Submitted` exists as a page in the Piratenwiki, protected against manual edits, and follows every later change in the portal. Motions must never again exist only in an external portal.

### Target format

The Piratenwiki runs MediaWiki 1.35 with the Action API at `https://wiki.piratenpartei.de/api.php`. The wiki sits behind a bot filter that blocks browser-like clients, so every request sends its own User-Agent, for example `Quartermaster/4 (+<public base url>)`. Motion pages are template calls. The existing templates and their parameters:

- `Satzungsänderungsantrag BPT` (SÄA), `Programmantrag BPT` (GP, WP), `Sonstiger Antrag BPT` (PP, SO, SBA).
- Parameters in use: `bundesparteitag`, `autor`, `antragstellerID`, `unterstützer1` to `unterstützer5` with `unterstützer1ID` and so on, `antragstyp`, `antragsgruppe`, `titel`, `text`, `begruendung`, `diskussionVorher`, `schlagworte`, `zusammenfassung`, `konkurrenz`, `prüficon`, `abstimmung`, `wikiBenutzer`, `antragsdiskussion`, `artantrag`.

Page title: `{WikiPagePrefix}/{Code}{Number:000}`, for example `Antrag:Bundesparteitag 2027.1/Antragsportal/SÄA001`.

The page body is rendered from the motion type's `WikiTemplate` (Fluid). Ship default templates for the six default types that produce the template calls above. Available variables: assembly label, type code and name, number, title, section, text and reason as wikitext, summary, keywords, prior discussion, competing motion numbers, check icon value, vote result value, the public portal URL of the motion, the bot user name.

Personal data: the wiki is public, so the mirror writes no names and no member numbers. `autor` is set to a fixed text that points to the portal ("Im Antragsportal einsehbar (Login erforderlich)") together with the motion's portal URL; the `antragstellerID` and `unterstützer` parameters are omitted.

Status mapping:

| Portal state | `prüficon` |
|---|---|
| Submitted, no open inquiry | 1 (ungeprüft) |
| Submitted, open inquiry | 7 (nachfragen) |
| Admitted | 2 (geprüft) |
| FormallyInsufficient | 5 (formal ungenügend) |
| Withdrawn or Lapsed | 6 (zurückgezogen) |

| Portal state | `abstimmung` |
|---|---|
| Result None or NotHandled | 1 (noch nicht abgestimmt) |
| Accepted | 2 |
| Rejected | 3 |
| Withdrawn or Lapsed | 4 |
| AcceptedModified | 5 |
| FormallyInsufficient | 8 |

### Markdown input and conversion for the wiki

Motion text and reason are written in a restricted Markdown: CommonMark plus pipe tables and strikethrough. Raw HTML and images are rejected with a validation message; the editor preview uses the same restricted flavour, so members see exactly what will be published. Other formatting is rendered as plain text.

For the wiki the Markdown is rendered to sanitized HTML and converted to MediaWiki text: the HTML tags MediaWiki accepts (paragraphs, line breaks, bold, italic, strikethrough, lists, headings, block quotes, code, horizontal rules, tables) are kept without attributes, links become external wiki links, every other tag is dropped and only its text kept.

Because the output is placed inside a template parameter, all member text is neutralized: `|` becomes the numeric entity `&#124;` (not `{{!}}`, which turns back into a live pipe once the template is expanded), `!`, `"`, `=`, `*`, `#`, `;` and `:` become entities as well, and braces, square brackets, angle brackets, `~`, `'` and `_` become HTML entities, so member-written text can never inject template calls, parameters, links, tags, signatures or magic words. Cover this with tests, including hostile input. The input is normalized to Unicode NFC first, because MediaWiki normalizes submitted text and a combining character must not merge with emitted markup. Motion text and reason are limited to 100,000 characters each, and text that the Markdown parser refuses as too deeply nested is rejected when saving.

### What is mirrored and when

- The wiki always shows the current text and status. A motion in revision gets a notice above the template call ("Dieser Antrag wird überarbeitet und sucht erneut Mitantragsteller.") with the portal link.
- A sync is queued on: first submission (page creation), every content change of a numbered motion (including the change that puts it into revision), resubmission, commission edit, formal check decision, inquiry opened or closed, competition added or removed, withdrawal, lapse of a previously submitted motion, result change.
- Drafts and motions that never reached `Submitted` are never written to the wiki.
- A withdrawn or lapsed motion keeps its page, with the status values above.

### Mechanics

- Options (global): `wiki.api_url`, `wiki.bot_username`, `wiki.bot_password` (secret, bot password of a dedicated wiki account), `wiki.protection_level` (default `Antrag`; the wiki has a protection level of that name).
- The client sends the User-Agent described above on every request.
- A singleton client logs in with the bot password (`action=login` with a login token), fetches a CSRF token and keeps the session; on an expired session it logs in again once and retries.
- Write with `action=edit`, `bot=1` and an edit summary naming the reason and the portal revision, for example "Antragsportal: Revision 3 eingereicht".
- After creating a page, protect it with `action=protect` for `edit` and `move` at the configured level. If protection fails, the page still counts as written and the motion gets `WikiSyncState = ProtectionFailed`, which the dashboard shows.
- Sync is asynchronous. State changes set `WikiSyncState = Pending` in the same transaction as the change; a hosted background service picks up pending motions, renders the current wikitext and writes it. Failures set `Failed` with `WikiLastError` and are retried with backoff. A user-facing action never fails or waits because the wiki is down.
- The portal is the source of truth. Before writing, the service compares the hash of the current wiki content with `WikiSyncedHash`. If they differ, someone edited the page by hand: overwrite it and write an audit log entry with the page title.
- A reconcile job runs once per night for every portal with the mirror enabled whose event is not archived: for every motion that was ever submitted it verifies that the page exists and matches, and queues a sync where it does not.
- The dashboard shows the sync state per motion, a count of failed syncs for the portal, and buttons to resync one motion or all motions.

### Prerequisites outside Quartermaster

A wiki account for the bot, a bot password for it with the rights to edit, create and protect pages, and membership in the wiki group that may edit pages protected at the configured level. Until these exist the mirror cannot be enabled; enabling it validates the credentials with a login and reports the result.

## OpenSlides Push

OpenSlides 4.3.2 has no motion import (neither a CSV import dialog nor a backend import action). Motions are therefore pushed into a configured OpenSlides meeting through the OpenSlides action API, by a manual commission action.

### Connection

- Options (global): `openslides.url` (base URL of the OpenSlides instance, for example `https://openslides.example.org`), `openslides.username`, `openslides.password` (secret). The account is a local OpenSlides account that has the motion manage permission in the target meetings.
- Login: `POST {url}/system/auth/login` with JSON `{"username": ..., "password": ...}`. The access token comes back in the `Authentication` response header, the refresh token as the `refreshId` cookie.
- Actions: `POST {url}/system/action/handle_request` with the JSON body `[{"action": "<name>", "data": [ ... ]}]`, the `Authentication` header and the `refreshId` cookie. The response has `success`, `message` and `results` (one list per action with the created ids). A response that carries a new `Authentication` header replaces the stored access token. On a 401 the client logs in again once and retries.
- The portal settings offer "Verbindung prüfen" for OpenSlides, which performs a login and reports the result.

### What is pushed

- The commission chooses the content: all `Admitted` motions, optionally also `Submitted` ones.
- Order: the prioritization ranking when it is enabled and has ended, otherwise by type `SortOrder` and number. New motions are created in this order, so their OpenSlides sequential numbers follow it. `motion.sort` is not used, because it requires every motion of the meeting.
- Per motion type, the first push creates a category with `motion_category.create` (`meeting_id`, `name` = type name, `prefix` = type code) and stores its id in `OpenSlidesCategoryId`.
- A motion without `OpenSlidesMotionId` is created with `motion.create`: `meeting_id`, `title`, `number` (the displayed number, for example `SÄA001`), `text` (`TextHtml`), `reason` (`ReasonHtml`, omitted when empty), `category_id`, and `additional_submitter` (the submitter names joined with commas, author first). Submitters are not linked to OpenSlides users.
- A motion that was pushed before and whose `Revision` is greater than `OpenSlidesPushedRevision` is updated with `motion.update` (`id`, `title`, `text`, `reason`, `additional_submitter`). Unchanged motions are skipped. Nothing is ever deleted in OpenSlides; a motion withdrawn after the push stays there and the commission handles it in OpenSlides.
- Each motion is sent as its own request, so one failure does not stop the others. Failures are stored in `OpenSlidesLastError`; the push result lists created, updated, skipped and failed motions, and the dashboard shows the push state per motion.

### JSON export

The commission can also download all motions from `Submitted` on as JSON with all public fields, the submitter names, rank and upvote count, for other consumers.

## Notifications

New triggers in `NotificationTriggers`, each with a payload, a recipient resolver and default templates, following the existing triggers:

| Trigger | Recipients |
|---|---|
| `assembly_motion_cosubmitter_reset` | Each removed co-submitter, with a link to rejoin |
| `assembly_motion_collecting_reminder` | The author, 48 hours before the effective deadline, if the motion is still collecting |
| `assembly_motion_submitted` | The commission of the event's chapter |
| `assembly_motion_lapsed` | Author and former co-submitters |
| `assembly_motion_checked` | All submitters, with decision and note |
| `assembly_motion_inquiry_asked` | All submitters |
| `assembly_motion_inquiry_answered` | The commission |
| `assembly_motion_edited_by_commission` | All submitters, with the change note |
| `assembly_motion_competition_declared` | Submitters of the other motion |
| `assembly_motion_withdrawn` | Co-submitters and the commission |

Recipients are resolved to the users linked to the members, so each person's channel preferences apply.

## Permissions

One new chapter-scoped permission: `assemblymotions_manage`. It lets the holder work as the motion commission for events of that chapter: see the dashboard, member numbers and inquiries; decide the formal check; ask and close inquiries; edit motions; withdraw on request; manage competition links; record results; trigger wiki resyncs; push to OpenSlides; download the JSON export.

It is not part of `DefaultOfficerPermissions`. Seed a system role `motion_commission` that contains it, so a board can appoint its commission by assigning the role.

Enabling and configuring the portal of an event (dates, types, wiki settings) requires the existing `events_edit` permission for the event's chapter.

Member actions (create, join, upvote, answer inquiries) need no permission; they are gated by `AssemblyMotionEligibility` and by being a submitter where that applies.

## API

Endpoints follow the one-endpoint-per-class FastEndpoints pattern with request validators, under `/api/assemblymotions`. Grouped by audience:

- Anonymous: portal info for an event; list and detail of public motions; competition links; ranking after the window.
- Eligible member: list of motions collecting co-submitters; member detail (with names, history); create, update, release, withdraw own motion; join and leave; declare and remove competition; upvote and remove upvote; own motions and own upvotes; answer an inquiry.
- Commission: dashboard list; formal check; edit with change note; inquiries (ask, close); results (single and bulk); withdraw; clear competing flag; wiki resync (one, all) and sync status; OpenSlides push; JSON export.
- Event editor: enable portal, update portal settings, create, update and delete motion types, validate wiki and OpenSlides credentials.

All list endpoints are paginated with the existing `IPaginatedRequest` and `IPaginatedResponse`.

## UI

German display strings through the existing i18n keys. No `@code` blocks.

Public and member pages:

- Portal page of an event: intro, deadlines per type, motions grouped by type with status badges, filter by type and status, search by title and number. Linked from the public event detail page.
- Motion detail: number, type, title, section, text, reason, summary, status, check note, result, competing motions. For signed-in eligible members also submitters, revision history, and the actions that apply (join, leave, upvote, edit, withdraw, declare competition, answer inquiry).
- Motions collecting co-submitters.
- Motion editor: type selection with the type's deadline and required submitters shown, the fields from the data model, `MarkdownEditorWithPreview` for text and reason, save as draft, release.
- "Meine Anträge": own motions and co-submissions with status and open inquiries.
- Ranking page after the prioritization window.

Administration pages:

- Portal settings as a section of the event detail page: enable, dates, prioritization window, types, wiki settings and the OpenSlides meeting id, each with a "Verbindung prüfen" button.
- Commission dashboard and commission motion detail (formal check, inquiries, edit, results, wiki state).
- Results table for bulk entry.

## Audit Log

Write audit entries for: portal enabled and settings changed; motion released, submitted, edited (Markdown diff as for board motions), withdrawn, lapsed; co-submitter joined, left, removed by reset; formal check decisions; inquiries asked, answered, closed; competition declared and removed; results set; wiki overwrite of a manual edit; wiki sync failures; OpenSlides pushes (with counts); JSON exports downloaded. Upvotes are not written to the audit log.

## Error Handling

- Validation errors return field messages in German as elsewhere.
- Actions refused by state or deadline return a clear message naming the reason ("Die Frist für diese Antragsart ist am 12.03. um 23:59 Uhr abgelaufen.").
- Concurrent edits: update requests carry the revision they were based on; a mismatch is refused with a message to reload.
- Wiki failures never surface as errors to members; they appear on the commission dashboard.

## Testing

TUnit tests in `Quartermaster.Server.Tests`, following the existing endpoint and validator test structure. At minimum:

- Eligibility: own chapter, descendant chapter, foreign chapter, exited, pending, floating, no linked member.
- Lifecycle: release with one and with five required submitters; the fifth join submits; joins refused after the deadline; lapse job; competing deadline.
- Numbering: sequence per type, no reuse after withdrawal, concurrent submissions.
- Editing: reset of co-submitters and notifications; return to collecting; author edits refused after check and after deadline; commission edit keeps co-submitters; revision conflict.
- Formal check and inquiries: minimum response time, visibility.
- Competition: symmetry, removal rights, competing flag.
- Prioritization: window bounds, one upvote per member, counts hidden during the window, ranking order and tie breaks.
- Privacy: anonymous responses contain no names; member numbers only for the commission.
- Markdown input: raw HTML and images rejected; the restricted flavour renders tables and strikethrough.
- Wiki: Markdown to MediaWiki conversion including injection attempts; template rendering per type; status mappings; sync queue with a faked wiki client (create, update, protection failure, manual edit detected, retry after failure); reconcile.
- OpenSlides: push with a faked client (category creation once per type, create, update only after a revision change, skip unchanged, one failure does not stop the rest, ordering); the HTTP client against a faked message handler (login header and cookie, token replacement, re-login on 401).
- JSON export: content and ordering.

## Changelog

Add a section to the changelog file of the version this ships in, in German, describing only what users see: the motion portal for assemblies, co-submitting, prioritization, and the commission tools.

## Suggested Build Order

Each step leaves the application working.

1. Portal settings and motion types on events; entities and migration; eligibility; drafting, releasing, co-submitting, submitting, numbering; public and member views.
2. Editing rules and revisions; commission dashboard, formal check, inquiries, commission edits; notifications.
3. Competition; prioritization and ranking; results.
4. Wiki mirror: converter, templates, client, sync service, reconcile, dashboard state.
5. OpenSlides push and JSON export.
