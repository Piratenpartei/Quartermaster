# Configuration

Reference for all runtime settings. For initial server setup see [Installation.md](Installation.md).

## Where settings live

Quartermaster splits configuration across two layers:

| Layer | What goes there | How it's edited |
|---|---|---|
| `appsettings.Production.json` | DB connection string, trusted reverse-proxy IPs, log levels | Edit file, restart service |
| **Options table** (DB-backed) | Everything else — SMTP, branding, SAML/OIDC, rate limits, etc. | Web UI: `/Administration/Settings`. Hot-reload, no restart |

A handful of Options are **per-chapter overridable**: setting them at the chapter level overrides the global default for users scoped to that chapter and its descendants. The rest are global only.

All Options have built-in defaults — the app boots without any of them being set. Anything required for a specific feature (email, SSO, Telegram) is called out below.

## appsettings.Production.json

### Database

```json
"DatabaseSettings": {
  "ConnectionString": "server=db.internal;user id=quartermaster;password=...;database=quartermaster;pooling=true;max pool size=20;min pool size=5;"
}
```

`max pool size` is per-instance. For a small chapter (< 100 concurrent users) the defaults are plenty; raise both pool numbers if you see `Timeout expired` errors in the logs.

### Reverse proxy

```json
"ForwardedHeaders": {
  "KnownProxies": ["127.0.0.1"],
  "KnownNetworks": []
}
```

X-Forwarded-* headers are processed **only** for connections coming from the listed proxies / networks. With both lists empty the headers are dropped entirely (this is the safe default — a misconfigured proxy can't spoof client IPs).

- `KnownProxies` — exact IPv4/IPv6 addresses, e.g. `127.0.0.1`, `::1`, `10.0.0.5`
- `KnownNetworks` — CIDR notation, e.g. `10.0.0.0/8`, `192.168.1.0/24`

Both lists are additive. The client IP that ends up in audit logs and rate-limit buckets is what the *last* trusted proxy in the chain reports — so make sure your proxy is configured to **append** to `X-Forwarded-For` rather than overwriting it.

## Global options (DB-backed)

Settings below are edited at `/Administration/Settings`. The "Identifier" column is what appears in import/export and audit logs.

### System branding & links

| Identifier | Default | Purpose |
|---|---|---|
| `system.app_name` | `Quartermaster` | Display name used in branding and outgoing notification subjects |
| `system.public_base_url` | _empty_ | Externally-reachable URL **without trailing slash** (`https://quartermaster.example.de`). Empty = direct links in notifications are skipped |
| `general.chaptername.display` | _empty_ | Umbrella organisation display name (per-chapter overridable) |
| `general.contact.email` | _empty_ | Contact address shown in error pages and notification footers (per-chapter overridable) |
| `general.error.contact` | German default fallback | Hint text shown on error pages (per-chapter overridable) |
| `general.error.show_details` | `false` | `true` reveals technical detail in error UI — leave off in production |

`system.public_base_url` is required for every outbound notification that contains a link (application-received mails, due-selection confirmations, motion notifications, etc.). Set it before going live.

### SMTP (outgoing email)

Required for: all email notifications, including the membership-application confirmation flow.

| Identifier | Default | Purpose |
|---|---|---|
| `email.smtp.host` | _empty_ | SMTP server hostname (e.g. `smtp.example.com`) |
| `email.smtp.port` | `587` | `587` for STARTTLS, `465` for implicit SSL |
| `email.smtp.username` | _empty_ | SMTP auth username |
| `email.smtp.password` | _empty_ | SMTP auth password (stored masked) |
| `email.smtp.use_ssl` | `true` | Encrypted connection — leave `true` unless using a local relay |
| `email.smtp.sender_address` | _empty_ | `From:` address |
| `email.smtp.sender_name` | `Quartermaster` | `From:` display name |
| `email.smtp.batch_size` | `50` | Max messages per opened SMTP connection. Larger = fewer reconnects, smaller = faster recovery on transient failure |

Use the "Test SMTP" button in the Settings UI to validate before relying on it for member-facing flows.

### Telegram (optional)

Required for: pushing notifications to a Telegram bot in addition to email.

| Identifier | Purpose |
|---|---|
| `messaging.telegram.bot_token` | Bot API token from `@BotFather` (stored masked). Empty = Telegram channel is disabled |
| `messaging.telegram.bot_username` | Bot username without leading `@`. Used to build `https://t.me/<bot>?start=<token>` deeplinks for account linking |

### PDF print-outs

| Identifier | Default | Purpose |
|---|---|---|
| `messaging.pdf.output_dir` | `./data/printouts` | Absolute path for generated PDF letters. Make sure the service user can write here |

### SAML SSO (optional)

Required for: third-party SSO login via SAML 2.0 (e.g. Keycloak, ADFS, Authentik).

| Identifier | Purpose |
|---|---|
| `auth.saml.endpoint` | IdP SSO endpoint URL |
| `auth.saml.client_id` | Service-provider entity ID / client ID registered with the IdP |
| `auth.saml.certificate` | Base64-encoded IdP signing certificate (no BEGIN/END headers). Stored masked |
| `auth.saml.button_text` | Login button label (default: `SSO Login`) |
| `auth.saml.expected_audience` | Required `<Audience>` value in the assertion. Empty = skip check (not recommended) |
| `auth.saml.expected_destination` | Required `<Destination>` URL — usually `<public_base_url>/api/users/SamlConsume`. Empty = skip check |
| `auth.saml.member_number_attribute` | SAML attribute carrying the member number (default: `member_number`). Users are linked to members by this number; when no member exists yet, one is created from first name, last name, email and member number, and the next member import fills in the rest |

### OpenID Connect (optional)

| Identifier | Purpose |
|---|---|
| `auth.oidc.authority` | OIDC authority URL (e.g. `https://keycloak.example.com/realms/master`) |
| `auth.oidc.client_id` | OIDC client ID |
| `auth.oidc.client_secret` | OIDC client secret (stored masked) |
| `auth.oidc.button_text` | Login button label (default: `OpenID Login`) |
| `auth.oidc.member_number_claim` | ID-token claim carrying the member number (default: `member_number`). Linking and member creation work as for SAML; first and last name come from `given_name` / `family_name` |

### News Posts (optional)

Optional. When enabled, the app polls the configured news feeds, matches items against the party programme, and drafts social-media post candidates via Mistral for review before publishing them through Postiz.

| Identifier | Default | Purpose |
|---|---|---|
| `newsposts.enabled` | `false` | Turns feed polling and candidate generation on/off |
| `newsposts.poll_interval_minutes` | `15` | How often feeds are polled and processed |
| `newsposts.max_item_age_hours` | `48` | Items older than this are ignored on fetch |
| `newsposts.retention_days` | `30` | Items with no publication are deleted after this many days without change |
| `newsposts.mistral.api_key` | _empty_ | Mistral API key (stored masked) |
| `newsposts.mistral.base_url` | `https://api.mistral.ai` | Mistral API base URL |
| `newsposts.mistral.triage_model` | `mistral-small-latest` | Model used for relevance triage |
| `newsposts.mistral.generation_model` | `mistral-medium-latest` | Model used for candidate generation |
| `newsposts.mistral.embedding_model` | `mistral-embed` | Model used for programme-section embeddings |
| `newsposts.triage.min_similarity` | `0.70` | Below this cosine similarity to the programme, an item is treated as irrelevant without a Mistral call (0 to 1) |
| `newsposts.generation.candidate_count` | `5` | Number of post candidates generated per item |
| `newsposts.generation.prompt` | _default prompt_ | Tone and style rules for generated posts; format and factual rules are added by the app itself |
| `newsposts.postiz.base_url` | _empty_ | Postiz instance base URL including `/api`, e.g. `https://postiz.example.org/api` |
| `newsposts.postiz.api_key` | _empty_ | Postiz API key (stored masked) |
| `newsposts.postiz.x_reply_setting` | `everyone` | Who may reply on X: `everyone`, `following`, `mentionedUsers`, `subscribers`, or `verified` |

### Keycloak (Akkreditierung)

Optional. Lets delegates at Bundesparteitage look up member accounts in Keycloak by member number, set voting eligibility, and create temporary accounts for walk-in members.

| Identifier | Default | Purpose |
|---|---|---|
| `keycloak.base_url` | _empty_ | Base URL of the Keycloak server, e.g. `https://sso.piratenpartei.de` |
| `keycloak.auth_realm` | `master` | Realm the admin client authenticates against |
| `keycloak.realm` | _empty_ | Realm the member accounts live in |
| `keycloak.client_id` | _empty_ | Client with client credentials and the `manage-users` role in the member realm (same client as MemberDataImport) |
| `keycloak.client_secret` | _empty_ | Client secret (stored masked) |
| `keycloak.username_separator` | `.` | Separator between first and last name, same as MemberDataImport |

The client needs the `manage-users` client role of `<realm>-realm` (or of `realm-management` when the client lives in the member realm), same client as MemberDataImport.
In the target realm, set Realm settings > General > Unmanaged attributes to Enabled, same as MemberDataImport's deployment notes; otherwise searches return users without `member_number` and every lookup reports "no account".

### Wiki mirror for the motion portal

Optional. When "Enable wiki mirror" is switched on in the portal settings ("Motion portal" on the event), every motion that was submitted at least once is written to the Piratenwiki as a page and kept up to date. The pages are protected against manual edits. The wiki receives no names and no member numbers: the `autor` parameter is a fixed text pointing to the motion in the portal.

| Identifier | Default | Purpose |
|---|---|---|
| `wiki.api_url` | `https://wiki.piratenpartei.de/api.php` | URL of the MediaWiki Action API. Must be `https`; plain `http` is only accepted for `localhost`, `127.0.0.1` and `[::1]` |
| `wiki.bot_username` | _empty_ | Wiki login of the bot, for a bot password in the form `User@BotName` |
| `wiki.bot_password` | _empty_ | Bot password (stored masked) |
| `wiki.protection_level` | `Antrag` | Protection level applied to created pages (edit and move) |

Prerequisites outside Quartermaster:

- A dedicated wiki account for the bot and a bot password with the rights to edit, create and protect pages. The account must be in the wiki group that may edit pages protected at the configured level.
- The protection level named in `wiki.protection_level` must exist on the wiki (`$wgRestrictionLevels`). Otherwise creating pages works but protecting them fails and the motion shows "Protection failed".
- The three BPT templates `Satzungsänderungsantrag BPT`, `Programmantrag BPT` and `Sonstiger Antrag BPT` must exist on the wiki.
- `system.public_base_url` must be set, because every page links back to the motion in the portal.

Every request sends the User-Agent `Quartermaster/4 (+https://piratenpartei.de)`. The wiki sits behind a bot filter that blocks browser-like clients; if the filter rejects this User-Agent the connection check reports that the wiki blocked the request.

"Check connection" in the portal settings logs in once with the stored credentials and shows the result. It never reads, edits or protects a page. Switching "Enable wiki mirror" on runs the same login first and refuses with the wiki's reason when it fails or when the options are not set; on success all numbered motions of the portal are queued.

Which motions are mirrored and when:

- Only motions that have a number (submitted at least once) get a page. Drafts and motions that are still collecting co-submitters are never written.
- The page title is `<Wiki page prefix>/<type code><number>`, for example `Antrag:Bundesparteitag 2027.1/Antragsportal/SÄA001`.
- A sync is queued on first submission and on every later change that is visible on the page: content change, resubmission, commission edit, check decision, inquiry opened or closed, competition added or removed, withdrawal, lapse and result change. A motion in revision gets a notice above the template call.
- A background job writes queued motions about once a minute, at most 20 per run. A user action never waits for the wiki. Failures are retried with a growing delay of up to six hours. A reconcile job starts ten minutes after the server starts and then runs every 24 hours. Each run compares at most 500 synced pages with the stored state, rotating through all pages by day, and queues pages that were changed by hand or deleted.
- "Synchronize again" on a motion and "Synchronize all again" on the dashboard only queue motions and reset their retry delay, so the next job run tries them even after a permanent failure. They are refused while the mirror is disabled.

A wiki page that already exists under the target title and was not created by the portal is never overwritten. The motion then shows "Failed" and the last error explains it. Resolve it by changing the "Wiki page prefix" or by moving or deleting the foreign page on the wiki, then use "Synchronize again".

Switching "Enable wiki mirror" off writes nothing to the wiki. Existing pages stay as they are and the motions keep their last state; they are no longer updated. Switching it on again queues all numbered motions.

The page body is rendered from the "Wiki template (Fluid)" of the motion type. When the field is empty the built-in default for the type code is used. Available variables; all text values are already escaped for the wiki, so print them without further filters:

- `assembly.Label`: the "Assembly label in the wiki" of the portal.
- `type.Code`, `type.Name`: code and name of the motion type.
- `motion.Number`: display number such as `SÄA001`.
- `motion.Title`, `motion.Section`, `motion.Summary`, `motion.Keywords`, `motion.PriorDiscussion`: single-line texts.
- `motion.Text`, `motion.Reason`: the motion text and reason converted to wikitext.
- `motion.CompetingNumbers`: display numbers of competing motions, comma separated.
- `motion.CheckIcon`: value for `prüficon` (1 unchecked, 2 admitted, 5 formally insufficient, 6 withdrawn or lapsed, 7 open inquiry).
- `motion.VoteResult`: value for `abstimmung` (1 not yet voted, 2 accepted, 3 rejected, 4 withdrawn or lapsed, 5 accepted modified, 8 formally insufficient).
- `motion.PortalUrl`: public URL of the motion in the portal.
- `motion.InRevision`: true while the motion is in revision.
- `bot.UserName`: the configured bot user name.

First run checklist:

1. Set the three options in `/Administration/Settings` and press "Check connection".
2. Enable the mirror with a test prefix in the bot's own user namespace, for example `Benutzer:<Bot>/Antragsportal-Test`, and not with the real `Antrag:` pages. Let the job write a few pages and check them on the wiki.
3. Only after the test pages look right, change "Wiki page prefix" to the real prefix and save. Saving a changed prefix on an enabled mirror queues all numbered motions, so the pages appear under the new prefix within a minute or two; "Synchronize all again" on the dashboard forces it. Pages under the old prefix are not touched, stay on the wiki and have to be deleted there by hand.

Inputs to submit on the first real run and what to look for. Use the test prefix, a type with the real BPT template, and enter the text in the motion's title or text field:

1. Title and first text line `x style="color:red" | hello || more !! x`: shown literally, no red cell, no extra table cells.
2. Titles, one per run: `* a`, `# a`, `; a : b`, `: a`, `= a =`, `- a`, `----`, `{| x`: shown literally, no list, definition term, heading, rule or table anywhere in the rendered template.
3. Text with five blocks `a | b`, `` `a | b` ``, a table row `| a \| b |` under `| h |` and `|---|`, `## a | b`, `[a | b](https://example.org/)`: a plain `|` is displayed in all five.
4. A fenced code block with the lines `a|b <x> &amp; {{y}} [[z]] ''q''`, ` leading space`, `* star`, `{| t`, `----` and `ISBN 1234567890`: one grey box with every line literal and no list, table or rule.
5. Text `[x](https://example.org/?a=1&b=2)`: the link target is exactly `https://example.org/?a=1&b=2`.
6. Text `[X](HTTPS://EXAMPLE.ORG/)`, `[Mail](mailto:a@example.org)` and `[](https://example.org)`: a link, a mail link and a numbered link `[1]`.
7. Text `see https://example.org and ISBN 1234567890 and RFC 1234 and PMID 1234`: no automatic link at all.
8. Text `# A` and `## B`: check whether the headings appear in the page's table of contents and how a first-level heading looks inside the template.
9. Text with a nested list, a quote with two paragraphs, a table and a hard line break: no stray empty paragraph, no indented preformatted box, table readable without `class="wikitable"`, no literal `thead` or `tbody`.
10. After the first save, open the page source: it must be identical to what the portal rendered, and a title or text containing `Antra`, `a`, U+0308, `ge` (decomposed) must come back as `Anträge` (U+00E4).
11. Text `&#x338;foo`, a code block starting with U+0338, a title starting with U+0338 and a title of U+037E followed by ` a`: everything renders as plain text and the template's title field is filled.
12. Text `[a&#xFFFD;b](https://example.org/)`: note whether it becomes a link or literal brackets.
13. Text `[x](https://example.org/?a=1)` as the whole text: fully displayed, which confirms that the template receives values as named parameters.
14. A text of 99000 `|` characters: the save succeeds and the page renders.

What the client assumes about the wiki, to confirm while doing the above:

- Login with `action=login` and a bot password (`User@BotName`) returns `Success`, a later `meta=tokens` query returns a CSRF token, and logging in again on an already logged-in session is accepted.
- The session cookie is kept across requests and the bot filter accepts the User-Agent on POST as well as GET.
- A missing page is reported as missing and an existing page returns its text from the main slot.
- An edit returns the result `Success`, optionally with `nochange`; the abuse filter, spam blacklist and captcha are reported as such.
- A protect call returns a `protect` object. A missing protection level or missing right shows up as an error code in the last error of the motion.
- Only `badtoken`, `assertuserfailed` and `notloggedin` are treated as an expired session and retried after a new login; errors such as `readonly`, `ratelimited`, `protectedpage`, `permissiondenied` and `maxlag` are shown as the last error and retried with the growing delay.
- Request bodies of up to several hundred KB are accepted (`post_max_size` and the 2 MB article size limit).
- The abuse filter does not block the bot account on its first page creations.

### OpenSlides push for the motion portal

Optional. The commission can push the motions of a portal to an OpenSlides 4.3 meeting with "Push to OpenSlides" on the motion dashboard. The push is started by hand and never runs in the background. The same dashboard offers "Export JSON", which downloads the numbered motions of the portal as a JSON file and needs no configuration. Drafts and motions that never had a number are not part of the file. Status, result and required majority are written as names, for example `Admitted`, `AcceptedModified` and `TwoThirds`.

| Identifier | Default | Purpose |
|---|---|---|
| `openslides.url` | _empty_ | Base URL of the OpenSlides instance, for example `https://openslides.example.org`. Must be `https`; plain `http` is only accepted for `localhost`, `127.0.0.1` and `[::1]` |
| `openslides.username` | _empty_ | Login of the account used for the push |
| `openslides.password` | _empty_ | Password of that account (stored masked) |

Prerequisites outside Quartermaster:

- An OpenSlides account that has the permission to manage motions in the target meeting. The push creates categories and motions and changes motions it created earlier.
- The meeting id of the target meeting. It is the number in the meeting's URL, for example `2` in `https://openslides.example.org/2/motions`. Enter it as "OpenSlides meeting ID" in the portal settings ("Motion portal" on the event) and save. "Check connection" next to it logs in once with the stored options, creates nothing and does not depend on unsaved form values.

What the push does:

- It sends the admitted motions. With "Also push submitted motions that have not been checked yet" ticked in the dialog it also sends motions with the status "Submitted". Motions without a number are never sent.
- The order is the prioritization ranking once the ranking is public, then all other motions by type sort order and number.
- Each motion type gets one OpenSlides category, created on the first push that needs it, with the type name as name and the type code as prefix. The category id is remembered, so later pushes reuse it.
- A motion that is not in OpenSlides yet is created with its title, number, text, reason, category and the submitter names as additional submitter text. A motion that was pushed earlier is updated when its revision is newer than the pushed one and left alone otherwise. A push never deletes anything.
- The result is shown on the dashboard as created, updated and unchanged counts plus one line per failed motion. A failed motion keeps its error as the last error on the motion's OpenSlides card and is tried again by the next push. Only one push per portal runs at a time.

Known limits:

- A motion that is withdrawn or becomes ineligible after a push stays in OpenSlides and has to be removed there by hand.
- A later change of a motion's type or number is not transferred to the pushed motion or its category.
- If OpenSlides creates a motion but the answer is lost, for example through a timeout, the next push creates the motion a second time. After a failed run, check OpenSlides for duplicates.
- Submitter names are sent as text. They are not updated when a member is anonymized later.
- When the browser request is cut off, because the client times out after 100 seconds or the tab is closed, the server stops the run before the next motion. Motions already transferred keep their OpenSlides id, and the next push continues with the rest without creating duplicates.
- Changing the meeting id of a portal that has already pushed is not supported: later pushes would update and categorize by ids of the old meeting.
- The guard against two simultaneous pushes of the same portal holds per server process only.

First run checklist:

1. Create a throwaway test meeting in OpenSlides and give the account the motion management permission there.
2. Set the three options in `/Administration/Settings`.
3. Enter the test meeting's id as "OpenSlides meeting ID" in the portal settings, save, and press "Check connection".
4. Push with and without the checkbox and compare the result with the meeting: categories, numbers, texts, reasons, submitter text and order.
5. Change a pushed motion, push again and confirm that the motion is updated and not duplicated.
6. Do this first run with a portal of a test event, not with the real one. The OpenSlides ids of pushed motions and categories are stored per portal and are not tied to a meeting id, so a portal that has pushed must never be pointed at a different meeting.

What the code assumes about OpenSlides 4.3, to confirm by a real run:

- A login with `POST /system/auth/login` and a JSON body of `username` and `password` returns the access token in the `Authentication` response header and sets a `refreshId` cookie. Both are sent on later requests.
- Actions are accepted at `POST /system/action/handle_request` with a body of the form `[{"action": "<name>", "data": [{...}]}]` and are answered with `success`, `message` and `results`, where the id of a created object is `results[0][0].id`.
- `motion_category.create` accepts `meeting_id`, `name` and `prefix`.
- `motion.create` accepts `meeting_id`, `title`, `number`, `text`, `reason`, `category_id` and `additional_submitter`.
- `motion.update` accepts `id`, `title`, `text`, `reason` and `additional_submitter`.
- A new `Authentication` header in an answer replaces the stored token, and a 401 is answered by one new login.

### SSO support contact

| Identifier | Purpose |
|---|---|
| `auth.sso.support_contact` | Contact info shown to users when SSO login fails (e.g. email or URL) |

### Authentication tuning

| Identifier | Default | Purpose |
|---|---|---|
| `auth.token.lifetime_days` | `7` | Login-token lifetime in days. Sliding-window — every successful use extends it |
| `auth.lockout.max_attempts` | `5` | Failed login attempts per (IP, user) tuple before lockout |
| `auth.lockout.duration_minutes` | `15` | Both the rolling window for attempt counting and the lockout duration after the threshold trips |

### Rate limiting (anonymous endpoints)

Shared bucket across all anonymous POST endpoints (motion create, membership application create, due-selection create). Hot-reloaded — takes effect for new IP buckets immediately and for active buckets after their current window expires.

| Identifier | Default | Purpose |
|---|---|---|
| `auth.ratelimit.anonymous_create_permits` | `5` | Max anonymous create requests per IP per window |
| `auth.ratelimit.anonymous_create_window_minutes` | `10` | Window length in minutes |

### Member import

Optional. When configured, the app periodically polls a CSV file (typically synced in from an external member database) and reconciles it against the local `Members` table.

| Identifier | Default | Purpose |
|---|---|---|
| `member_import.file_path` | _empty_ | Absolute path to the CSV. Empty = importer is idle |
| `member_import.polling_interval_minutes` | `10` | How often the file is hashed and (if changed) re-imported |

### Meetings

| Identifier | Default | Purpose |
|---|---|---|
| `meetings.protocol.archive_dir` | `./data/protocols` | Absolute path for archived meeting protocol PDFs |
| `meetings.collab.save_interval_seconds` | `10` | How often the server persists the collaborative-notes snapshot. Lower = less data loss on disconnect, higher = less DB write traffic |
| `meetings.motion_notes_template` | _Fluid template_ | Pre-filled note body for motion agenda items. Variables: `motion.Title`, `motion.AuthorName`, `motion.AuthorEmail`, `motion.Text` (per-chapter overridable) |

## Per-chapter overrides

The following options can be overridden per chapter; users see the override that matches the deepest chapter they're scoped to, falling back up the tree to the global value:

- `general.chaptername.display`
- `general.contact.email`
- `general.error.contact`
- `meetings.motion_notes_template`

Edit per-chapter values from the chapter detail page rather than the global Options UI.

## Notification templates

Email/Telegram/PDF templates for each trigger (application received, motion approved, etc.) are managed in **Administration → Templates**. They are not in the Options table — they have their own UI with preview, Fluid template syntax, and chapter-specific overrides. See `Quartermaster.Documentation/plans/` for the template engine design notes.
