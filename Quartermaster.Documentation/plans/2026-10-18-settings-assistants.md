# Settings Assistants (V5.1)

`/Administration/Settings` becomes a hub of assistant cards. The raw option table moves to `/Administration/Settings/Advanced`, reachable through a small link-button below the cards. No schema change, no migration.

## Catalog

`Quartermaster.Blazor/Pages/Administration/Settings/SettingsAssistantCatalog.cs` describes every assistant:

- slug, group, icon, title and description keys, route `/Administration/Setup/{Slug}`
- sections with a title, optional description, fields and an optional connection check
- fields carry a kind (text, number, bool, duration m:ss, seconds, minutes, multiline, select, suggest, base URL) and flags (full width, fill from check)
- `ManagedKeys` for options an assistant sets through its own flow (Mumble certificate and channels, public form switches)
- `RequiredKeys` for the card status; `ToggleKey` shows On/Off instead

`AdvancedOnlyKeys` lists the options without an assistant. A test asserts that every option definition is covered by an assistant or listed as advanced-only, and that every catalog key exists.

## Assistants

Basics:
- General: `system.app_name`, `system.public_base_url` (button "use current address"), `general.chaptername.display`, `general.contact.email`, `general.error.contact`; overridable fields link to their chapter overrides
- Public forms (own page): membership application, due selection, motion form with per-chapter overrides (on, off, inherit)

Login:
- SAML (existing) + `auth.saml.member_number_attribute`
- OIDC (existing) + `auth.oidc.member_number_claim`
- Login security: token lifetime, lockout attempts and duration, anonymous rate limit

Communication:
- SMTP (existing), `email.smtp.batch_size` moves to advanced
- IMAP and read receipts (existing)
- Telegram: token, username filled from `getMe`
- Member import: file path, interval, file check, link to import history

Integrations:
- Keycloak: all `keycloak.*`, check fetches a token and lists one user
- Piratenwiki: `wiki.*`, check logs in
- OpenSlides: `openslides.*`, check logs in
- Social media: Mistral (key, models with suggestions from the model list), Postiz (URL, key, X reply setting, check lists channels), pipeline (enabled, intervals, age, retention, similarity, candidate count, prompt); link to the feeds page

Assembly:
- Assembly assistant / Mumble (existing)
- Assembly procedure: speaking times (m:ss), grace period, start offer, Mumble messages, poll defaults, OpenSlides delegates group and projector IDs (plain IDs; reading them needs a per-meeting autoupdate subscription)

Advanced only: `general.error.show_details`, `email.smtp.batch_size`, `newsposts.mistral.base_url`, `assembly.voice_hangover_ms`, `meetings.collab.save_interval_seconds`, `meetings.motion_notes_template`, `messaging.pdf.output_dir`, `meetings.protocol.archive_dir`.

## Server

- `POST /api/settings/check` with a `SettingsCheckKind` (Telegram, MemberImport, Keycloak, Wiki, OpenSlides, Mistral, Postiz), gated by `EditOptions`, runs one `ISettingsCheck` against the saved values and returns success, a message key, a detail and items (models, channels, bot username)
- `IKeycloakAdminClient.CheckAccessAsync` and `IMistralClient.ListModelsAsync` are new
- `DELETE /api/options/{Identifier}/overrides/{ChapterId}` removes a chapter override (audit logged), used by the public forms page

## Client

- `OptionGroupEditor` takes catalog fields instead of plain keys and renders by kind
- Generic `SettingsAssistantPage` at `/Administration/Setup/{Slug}` renders catalog assistants; SMTP, IMAP, SAML, OIDC, the assembly assistant and public forms keep their own pages
- Hub cards show a status badge (configured, partial, not configured, on, off)
