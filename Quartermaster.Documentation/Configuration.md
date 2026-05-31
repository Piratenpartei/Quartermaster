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

### OpenID Connect (optional)

| Identifier | Purpose |
|---|---|
| `auth.oidc.authority` | OIDC authority URL (e.g. `https://keycloak.example.com/realms/master`) |
| `auth.oidc.client_id` | OIDC client ID |
| `auth.oidc.client_secret` | OIDC client secret (stored masked) |
| `auth.oidc.button_text` | Login button label (default: `OpenID Login`) |

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
