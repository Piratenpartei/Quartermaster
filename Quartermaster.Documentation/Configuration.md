# Configuration

Reference for all runtime settings. For initial server setup see [Installation.md](Installation.md).

## Where settings live

Quartermaster splits configuration across two layers:

| Layer | What goes there | How it's edited |
|---|---|---|
| `appsettings.Production.json` | DB connection string, trusted reverse-proxy IPs, log levels | Edit file, restart service |
| **Options table** (DB-backed) | Everything else — SMTP, branding, SAML/OIDC, rate limits, etc. | Web UI: assistants at `/Administration/Settings`, the full list at `/Administration/Settings/Advanced`. Hot-reload, no restart |

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

Settings below are edited at `/Administration/Settings`. The page shows one card per area (General, Public forms, SAML, OpenID Connect, Login security, Sending mail, Read receipts, Telegram, Member import, Keycloak, Pirate wiki, OpenSlides, Social media, Assembly assistant, Assembly procedure); each opens an assistant with the options of that area and a status badge on the card. Assistants for remote services have a "Save and check" button that saves the section and then tests the stored values (Telegram `getMe`, a read of the member import file, a Keycloak user listing, a wiki and an OpenSlides login, the Mistral model list, the Postiz channel list) without changing anything remote. The model fields of the social media assistant suggest the models the check found, and the Telegram check fills in the bot username.

"Advanced settings" below the cards lists every option with its identifier and the assistant that covers it. Options only reachable there: `general.error.show_details`, `email.smtp.batch_size`, `newsposts.mistral.base_url`, `assembly.voice_hangover_ms`, `meetings.collab.save_interval_seconds`, `meetings.motion_notes_template`, `messaging.pdf.output_dir` and `meetings.protocol.archive_dir`.

The "Identifier" column is what appears in import/export and audit logs.

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

### Motion portal deadlines

Deadlines (portal closing time, per-type and competing-motion deadlines, inquiry response times) are stored as the instant they end, in Berlin time. Enter a deadline that should cover a whole day as 00:00 of the following day: the portal shows "28.11.2026 00:00 (up to and including 27.11.2026 23:59:59)", so submitters see the last valid second while the stored value stays a plain instant.

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

1. Set the options in the "Pirate wiki" assistant under `/Administration/Settings` and press "Check connection" in the portal settings.
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
2. Set the three options in the "OpenSlides" assistant under `/Administration/Settings`.
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

### Assembly assistant (Mumble bot)

Optional. The assembly assistant connects to the Mumble server as a bot while an assembly session runs, tracks the request channels and moves members only when the chair clicks. It is set up under "Settings > Assembly assistant" (global permission `options_edit`); speaking times and poll defaults are in "Settings > Assembly procedure".

| Identifier | Default | Purpose |
|---|---|---|
| `mumble.host` | `mumble.piratenpartei.de` | Hostname of the Mumble server |
| `mumble.port` | `64738` | TCP port of the Mumble server |
| `mumble.username` | `Versammlungsassistent` | Name of the bot in Mumble; also the certificate's common name |
| `mumble.certificate` | _empty_ | PKCS#12 client certificate of the bot as Base64 (stored masked); generated on the setup page |
| `mumble.certificate_password` | _empty_ | Password of that certificate (stored masked) |
| `mumble.server_certificate_sha256` | _empty_ | Empty = the server certificate must be trusted by the system. Set = only a server certificate with this SHA-256 fingerprint is accepted (for a self-signed server certificate); "Check connection" shows the fingerprint |
| `mumble.channels.chair`, `.speaker`, `.motion_author`, `.pro`, `.contra`, `.question`, `.point`, `.listener` | _empty_ | Channel id per role; set through the channel mapping on the setup page |
| `assembly.times.presentation`, `.speech`, `.question`, `.statement`, `.closing_word`, `.candidate_presentation`, `.candidate_presentation_repeat`, `.candidate_question`, `.candidate_answer` | 300, 180, 30, 60, 180, 300, 180, 30, 120 | Default speaking times in seconds |
| `assembly.grace_seconds` | `30` | A request keeps its place in the queue for this long after the member left the channel or lost the connection |
| `assembly.voice_hangover_ms` | `500` | A member counts as no longer speaking after this long without a voice packet |
| `assembly.start_now_after_seconds` | `20` | The console offers "start now" for the speaking timer when the speaker has not spoken for this long |
| `assembly.messages.warning`, `assembly.messages.expired` | `Noch 30 Sekunden`, `Die Redezeit ist abgelaufen` | Mumble text messages to the speaker 30 seconds before and at the end of the speaking time |
| `assembly.openslides.delegates_group_id` | _empty_ | OpenSlides group that is entitled to vote in every poll |
| `assembly.poll.duration_seconds` | `70` | Default poll duration (60 seconds plus 10 seconds of stream delay) |
| `assembly.poll.auto_start`, `.auto_stop`, `.auto_publish` | `true` | Defaults for a new session; the chair changes them in the console during the session |

First setup:

1. On the setup page, enter server, port and bot name, then "Generate certificate". The certificate identifies the bot as the same registered user on every connection. Replacing it later makes the bot a new user on the server, so registration and rights have to be set up again.
2. "Check connection" connects once, shows the server version and the server certificate fingerprint, loads the channel tree and disconnects. If the server certificate is self-signed, copy the fingerprint into `mumble.server_certificate_sha256` and check again.
3. "Register bot" registers the bot on the server (the right SelfRegister, which `all` has by default on most servers; otherwise an admin registers it by hand).
4. Map each role to a channel and save. The check then reports unmapped roles, missing channels, a channel used for several roles and speaker, motion author / candidate ("Antragsteller") or listener channels that are not linked with the chair channel. The motion author / candidate channel is where the motion author or the candidate of the current item sits for presentation, answers, statement and closing word.

Rights on the Mumble server (set by a server admin; the bot never needs admin rights):

- The bot needs Move on the channel tree of the assembly ("Bundesparteitag"), with "Apply to subchannels", for example through membership in the group `bpt-team` or a dedicated group. Moving a member needs Move in the member's current channel and either Move in the target or Enter for the member there; members have no Enter in "Redner" and "Antragsteller", so the bot needs Move on both sides.
- The bot sits in the chair channel ("Versammlungsleitung", the linked one, not the internal room). It hears every channel reachable through links in which the speaker has Speak, which are the same voices the chair hears. It moves itself back when it is moved elsewhere.
- For tests, use a separate copy of the channel tree and map its channels; never test in the real assembly channels.

Running an assembly:

- Grant the system role "Versammlungsleitung" (`assembly_chair`, includes `assembly_view`) for the event's chapter to the chair and the chair helpers, and "Protokoll Versammlung" (`assembly_view`) to the minutes taker. The roles do not inherit to sub-chapters.
- The console is at `/assembly/{eventId}`, also reachable through "Versammlungsassistent" on the event page. "Start assembly" connects the bot; "End assembly" disconnects it and closes the session. Only one session runs at a time.
- An open session survives a restart of Quartermaster: on startup the bot reconnects, rebuilds the queues from the channels (unknown order is marked) and resumes the timer of the current speech. Do not deploy during an assembly.
- The procedure card follows the item: a motion starts with the presentation, an election with the candidate presentation, a general debate with the general debate; the chair switches the phase at any time. The suggestion (highlighted entry, space key) follows GO BPT §§ 12, 13 and 15: GO motions first, then questions, but neither while someone speaks or a poll runs; in the debate Pro and Contra alternate starting with Pro, five per side and round, unused slots do not pass over; in the general debate all requests in order of entry; in the candidate questioning at most five questioners. When no side has a free slot and a waiting request, the console shows the round end with "continue debate" (statement of the motion author, then the next round) and "closing word". A GO motion in the speaker channel opens "any objection?" with "accepted without objection", "objection" and "finished"; the interrupted phase and its counters continue afterwards. Speaking times follow the phase: presentation 5 min, answers of the motion author untimed, statement 1 min, closing word 3 min, candidate presentation 5 min (3 min when repeated), candidate answers 2 min, candidate questions 30 s.
- While a session runs, the connection check and the registration on the setup page are refused, because a second connection with the bot's certificate would push the live bot off the server.

OpenSlides polls:

- The session connects to OpenSlides when the event's motion portal has an "OpenSlides meeting ID" and the `openslides.*` access of the motion push is configured; otherwise the poll panel stays off and queues and timers work without it.
- The OpenSlides account of the push additionally needs a group in the meeting with `poll.can_manage` (polls on topics), `motion.can_manage_polls` (polls on motions) and `agenda_item.can_manage` (procedural topics). It must be a local account with a login, not SSO, because starting and stopping polls goes through the vote service with that account's session.
- `assembly.openslides.delegates_group_id` is the id of the group that may vote (the number in the group's URL in OpenSlides); it is set as the entitled group of every poll the assistant creates.
- The console shows a live connection to the meeting: running polls with the voting countdown and the number of votes cast, the latest results with an advisory evaluation (simple majority Ja > Nein, two-thirds Ja >= 2 x Nein for motion types that require it; abstentions never count), and the agenda of the meeting as a picker for the current item.
- The poll duration (default 70 seconds, i.e. 60 plus 10 for the stream delay) is set on the meeting's voting countdown right after a poll starts. With "stop when time is up" the poll is stopped when the countdown reaches zero, with "publish result" it is published right after. Both apply to polls created by hand in OpenSlides too. The settings are changed in the poll panel during the session; the options are only the defaults of a new session.

Poll templates and procedural topics:

- The poll panel creates standard polls on one click for the current agenda item (chosen from the OpenSlides agenda) and starts them when "start" is on: continue debate?, point of order (text), opinion poll (text), support (text and threshold, default 20), controversy (threshold 10), competition as acceptance vote and its runoff (only when the motion has competing motions in the motion portal), final vote, block vote, and for elections another round of questions. Titles carry the motion number.
- Procedural polls cannot hang on a motion, so they go onto the motion's procedural topic "<number>: Verfahren", a child of the motion's agenda item. The assistant finds it by title and creates it on first use; "create procedural topics for all motions" creates them in advance. For a plain topic item the poll goes onto that topic.
- Yes/No polls have the options "Ja" and "Nein", one vote, no abstain option. The final vote is a motion poll with Ja/Nein/Enthaltung (100 % base Ja + Nein); its evaluation follows the motion type. The competition poll allows one vote per motion and offers OpenSlides' general "No" as the valid empty ballot; the evaluation names the motion with the most votes if it has more than half of the valid ballots, otherwise a runoff is needed.
- Only polls created by the assistant in the running session can be reset or deleted from the console.
- `assembly.openslides.projector_ids` (comma-separated projector ids) and `assembly.poll.project` (default `false`, switchable in the poll panel) put every started poll and the voting countdown on those projectors.

Elections:

- On an election item the procedure card takes the candidates (one per line) and orders them by surname. "Presentation" next to a candidate marks them current and starts the presentation phase; "support (20)" creates the support poll for that candidate. The person in the motion author channel carries the role "Kandidatur" in console and overlay; the overlay marker "Wahlgang läuft" is switched in the card. The secret election itself runs in SecretBallot.

Minutes aid:

- The console lists the sessions of the event with their log. CSV (semicolon, UTF-8 with BOM, Berlin time) and Markdown (grouped by agenda item: speeches with role, duration and speaking time, polls with result, chair actions) are downloaded by everyone with `assembly_view`. A chair deletes the log of an ended session once the minutes are signed; the session row keeps the delete date.

OBS overlay:

- Add a browser source to the scene with the URL `https://<quartermaster>/assembly/{eventId}/overlay`, width 1920, height 1080. No login is needed; the page only shows what the stream audience sees anyway. The page background is already transparent; OBS's default custom CSS for browser sources can stay.
- `?element=` turns the source into a single element drawn at 0,0 in a fixed size, so the browser source can be set to exactly that size and moved, scaled and hidden per scene in OBS: `item` (agenda item, 1100x56), `speaker` (lower third with role and time, 780x180), `poll` (running poll with countdown and votes cast, then the result for 15 seconds, 700x150), `next` (next speakers per side, 400x260), `election` (election marker, 360x56). Without `element` the page is the full 1920x1080 layout with item, speaker, poll and election; `?next=true` adds the next-speakers column there.
- `?lang=en` switches the labels to English (default German regardless of the OBS browser's language).
- Finer adjustments without a code change go into the "Custom CSS" field of the browser source, for example `.assembly-overlay-lower-third { left: 1100px; bottom: 60px; }`. Classes (inside `.assembly-overlay` for the full layout, `.assembly-overlay-single` for a single element): `assembly-overlay-item`, `assembly-overlay-poll`, `assembly-overlay-lower-third`, `assembly-overlay-role`, `assembly-overlay-name`, `assembly-overlay-bar-fill`, `assembly-overlay-time`, `assembly-overlay-next`, `assembly-overlay-election`; colours as variables on that container (`--overlay-orange`, `--overlay-panel`, `--overlay-warning`, `--overlay-danger`).
- The overlay shows nothing while no session runs or the connection is down, and reconnects on its own, also after a Quartermaster restart. The time turns orange from 30 seconds and red with the overrun after expiry.
- Fonts are the party's house fonts PoliticsHead and DejaRip from the Piratenwiki page "Pirate Design/Schriften", served by Quartermaster.

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

### Public forms

The public membership application, due selection and motion form ship switched off. A switched-off form disappears from the "Member portal" menu (the menu itself disappears when all three are off), its pages show a "not available" notice, and the server refuses submissions.

| Identifier | Default | Purpose |
|---|---|---|
| `public_forms.membership_application.enabled` | `false` | `true` opens the membership application form |
| `public_forms.due_selection.enabled` | `false` | `true` opens the due selection form |
| `public_forms.motion.enabled` | `false` | `true` lets the chapter take motions through the public form (per-chapter overridable) |

- The motion form only offers chapters that are switched on. A value set on a chapter also applies to its sub-chapters unless they set their own, so switching on a state chapter opens the form for all its districts; the menu entry shows as soon as one chapter is switched on.
- Holders of `applications_process` or `dueselections_process` (in any chapter) still reach and submit those forms while they are off, so officers can enter paper submissions. The board's own motion page (Board work, Motions, Create) keeps working for holders of `motions_edit`.
- The browser reads the switches once when the app loads; visitors see a change after reloading.
- The "Public forms" assistant saves each switch at once. Its chapter list sets a chapter to open or closed; "Inherit" removes the chapter's own value (`DELETE /api/options/{identifier}/overrides/{chapterId}`), so the chapter follows its parent again.

### Travel expenses

The travel expense form at `/Forms/TravelExpense` is open to everyone. Anonymous applicants keep their draft in the browser and attach receipts to the printout by hand; logged-in users keep drafts on the server and upload receipts, which are appended to the PDF. Receipts stay stored until their owner deletes them on `/Forms/Receipts`, so a route map can be reused for years.

| Identifier | Default | Purpose |
|---|---|---|
| `forms.receipts.max_per_user` | `100` | Receipts one person can keep at the same time; holders of the global permission `receipts_unlimited` are exempt |
| `forms.receipts.max_file_mb` | `10` | Largest receipt file (JPEG, PNG or PDF) in megabytes |

- Rates, deadlines, header lines and the printed rules text are kept per chapter as versions with a valid-from date at `/Administration/TravelExpenseRules`. An application uses the newest version valid at the start of the trip, from the chapter travelled for up to the root chapter, then the global default (the federal rules, seeded on first start). Presets for the federal, Bavarian, Baden-Württemberg and Lower Saxony forms are available as starting points; chapters without own rules, such as Hessen, use the federal rules.
- Editing a chapter's rules needs `travel_expense_rules_edit` on exactly that chapter; the global default needs it granted globally.
- The submission deadline and the cut-off date of the following year never block an application; a late application only carries a note for the chapter's treasurer.

### Rate limiting (anonymous endpoints)

Shared bucket across all anonymous POST endpoints (motion create, membership application create, due-selection create). Hot-reloaded — takes effect for new IP buckets immediately and for active buckets after their current window expires.

| Identifier | Default | Purpose |
|---|---|---|
| `auth.ratelimit.anonymous_create_permits` | `5` | Max anonymous create requests per IP per window, shared by motions, membership applications, dues selections and the PDFs of the expense and travel expense forms |
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
- `public_forms.motion.enabled`

Edit per-chapter values from the chapter detail page rather than the global Options UI.

## Administrative divisions (GeoNames)

Municipalities and postcodes come from GeoNames (CC BY 4.0) and ship as two files beside the server binary:

- `DE_Base.txt`: `DE.txt` from https://download.geonames.org/export/dump/DE.zip
- `DE_PostCodes.txt`: `DE.txt` from https://download.geonames.org/export/zip/DE.zip

On startup and then daily the server hashes both files and imports them when they changed. Renamed units are updated in place; a removed unit is replaced by the unit that now holds its postcode (or its parent), and members and chapters pointing at it move along; units without a replacement are marked orphaned. The import history is under System, Import status. German municipal reforms mostly take effect on 1 January, so refresh the files early in the year.

The license requires naming GeoNames as the source: the "Sources & licenses" page (`/Attributions#geonames`, linked from the footer) does that, and the municipality search links to it.

## Notification templates

Email/Telegram/PDF templates for each trigger (application received, motion approved, etc.) are managed in **Administration → Templates**. They are not in the Options table — they have their own UI with preview, Fluid template syntax, and chapter-specific overrides. See `Quartermaster.Documentation/plans/` for the template engine design notes.
