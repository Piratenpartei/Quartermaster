# Accreditation: Design

## Goal

During an online assembly (OpenSlides behind the Keycloak SSO), a delegate for member data handles two cases from inside Quartermaster without touching the Keycloak admin console:

1. A member paid their dues after the early-morning member sync, so their Keycloak account is not yet eligible to vote. The delegate looks the account up by member number, confirms the data and sets the eligibility attribute to true.
2. A member cannot use the SSO because they have no account (no email in the member data). The delegate creates a temporary Keycloak account and passes the generated credentials on.

Both are manual, delegate-checked actions against the Keycloak admin API. Quartermaster stores nothing about these accounts except audit log entries.

## Interaction with MemberDataImport

MemberDataImport runs nightly and owns Keycloak membership data. This design relies on its existing behavior and changes nothing in it:

- It rewrites `ekklesia_eligible` from the CSV on every run. A manual `true` therefore lasts until the next run, by which time the CSV normally reflects the payment.
- It closes (disables, sets `closed_time`) every account whose `member_number` is not among the CSV rows that carry an email. A temporary account for a member without email is therefore closed by the next nightly run. That is the intended lifetime: temporary accounts are wholly temporary.
- If the delegate later adds the member's email to the source system, the next run matches the account by `member_number` (reactivating it if it was closed in the same year), which makes it permanent.
- Two accounts with the same `member_number` make the number ambiguous; MemberDataImport then skips that member and ignores both accounts. Accreditation therefore never creates an account for a member number that already has one (in any state).

## Scope

In scope: eligibility lookup and set-to-true; temporary account creation with a generated final password; the Keycloak admin client; options; one permission; audit logging.

Not in scope: setting eligibility to false; password resets or edits of existing accounts; editing names or emails; any change to MemberDataImport; groups for temporary accounts.

## Architecture

Code lives in `Quartermaster.Server/Accreditation/` (server), `Quartermaster.Api/Accreditation/` (DTOs) and `Quartermaster.Blazor/Pages/Administration/Accreditation.razor(.cs)` (UI).

```
KeycloakSettings            Typed read of the keycloak.* options
KeycloakTokenProvider       Client-credentials token, cached until shortly before expiry
IKeycloakAdminClient        Interface consumed by endpoints; faked in tests
HttpKeycloakAdminClient     FindByMemberNumber, UsernameExists, CreateUser, UpdateAttributes
KeycloakUserRepresentation  Minimal user JSON model (id, username, names, email, enabled, attributes)
AccreditationUsernameGenerator  MemberDataImport's normalization + hash-suffix rule, ported
ReadablePasswordGenerator   Three German words + two digits, cryptographically random
Endpoints/*                 Search, set eligibility, suggest username, create temporary account
```

The token provider is a singleton (one cached token per process); the admin client is scoped and uses `IHttpClientFactory`.

## Keycloak Admin API Usage

Base: `{base_url}/admin/realms/{realm}`. Token: `POST {base_url}/realms/{auth_realm}/protocol/openid-connect/token` with `grant_type=client_credentials`. The client needs the `manage-users` role of the target realm, the same client MemberDataImport uses.

- Search by member number: `GET /users?q=member_number:{n}&briefRepresentation=false&max=10`. Results are filtered client-side to exact `member_number` matches (trimmed), because `q` matching can be broader than exact.
- Username exists: `GET /users?username={u}&exact=true`.
- Create: `POST /users` with `username`, `firstName`, `lastName`, `email` (optional), `enabled: true`, `emailVerified: true` when an email is given, `attributes`, and `credentials: [{ type: "password", value: <generated>, temporary: false }]`. The new id comes from the `Location` header.
- Update attributes: `GET /users/{id}`, change only the target attribute in the loaded `attributes`, `PUT /users/{id}` with the representation sent back unchanged apart from that one attribute and without `enabled` (Keycloak reports a brute-force-locked user as `enabled: false`; sending it back would disable the account permanently). The eligibility action never changes whether an account is enabled; for disabled or locked accounts the page tells the delegate to refer the member to technical support. A legacy account whose username or email no longer passes Keycloak 26's user-profile validation then fails loudly with Keycloak's 400 on that PUT, rather than the update silently clearing the field.

Errors: a non-success status throws a `KeycloakException` with status and a truncated body (never the token or a password). 409 on create maps to "Benutzername oder E-Mail ist bereits vergeben."; 401/403 to "Quartermaster hat keine Berechtigung in Keycloak."; unreachable or timeout to "Keycloak ist nicht erreichbar.".

## Attributes

Attribute names and values match MemberDataImport (`mappers.json`, `CoreAttributes`):

```
member_number               the entered member number (trimmed)
ekklesia_verified           "true"
ekklesia_eligible           "true" or "false" (form checkbox, default true)
ekklesia_external_voting    "false"
created_time                UTC, format yyyy-MM-dd'T'HH:mm:ss.ffffff'+00:00' (MemberDataImport's SyncTimestamp)
```

Temporary accounts get no group.

Setting eligibility changes only `ekklesia_eligible` to `"true"`.

## Username Rule

Ported from MemberDataImport's `UsernameGenerator`:

- Base name: `{first}{separator}{last}`; dots and whitespace become the separator, runs collapse; Keycloak-prohibited characters ``<>&"'$%!#?§,;:*~/\|^=[]{}()` `` and control characters are dropped.
- If the base name is free (case-insensitive), use it; otherwise append `{separator}{hash}` where hash is the first 8 bytes of SHA-256 over the trimmed member number, big-endian, modulo 1,000,000, zero-padded to six digits.
- If both are taken, creation fails with a message asking the delegate to enter a username by hand.
- A username entered by hand is validated (no whitespace, prohibited or control characters, not empty) and checked for existence; it is never suffixed silently.

## Password Rule

Three words from a fixed list of short, unambiguous German nouns (no umlauts or ß, at least 300 words, embedded in code), joined by `-`, plus `-` and two digits, e.g. `Segel-Wolke-Hafen-47`. Words and digits are drawn with `RandomNumberGenerator`. The password is returned once in the create response, shown once in the UI, and never stored or logged.

## Page

`/Administration/Akkreditierung`, navigation entry "Akkreditierung" under "Vorstandsarbeit", both gated by `accreditation_manage`.

**Stimmberechtigung freischalten:** member number input and "Suchen".
- One match: card with username, first and last name, email, status (aktiv / deaktiviert / geschlossen when `closed_time` is set), current `ekklesia_eligible` and `ekklesia_verified`. If eligible is already true, a note instead of the button. Otherwise "Stimmberechtigt setzen", an in-page confirmation naming the person and number, then a success message. Disabled accounts show a warning that the person cannot log in, but the button stays available.
- No match: message pointing to the temporary-account section.
- Several matches: all listed, no action offered, note to contact IT.

**Temporären Zugang anlegen:** Vorname, Nachname, Mitgliedsnummer (required), Benutzername (pre-filled in the browser as `Vorname.Nachname` while typing, editable), E-Mail (optional), Stimmberechtigt (checked). Nothing is checked against Keycloak automatically: the delegate presses "Prüfen", which validates every field and asks Keycloak whether the member number already has an account and whether the username is free (offering a free variant to take over when it is not). "Zugang anlegen" is only enabled after a passing check and is disabled again by any later change; creating re-checks everything server-side.
- On submit the server rejects a member number that already has an account in any state, returning that account so the page can show it with a pointer to the first section.
- On success a box shows username and password with a copy button and the note "Wird nur jetzt angezeigt; Zugang wird in der kommenden Nacht automatisch geschlossen." The form resets.

All UI strings are German via i18n keys in `de.json` and `en.json`.

## Endpoints

All require `accreditation_manage` (global); 401 without login, 403 without the permission. Mutations go through the existing antiforgery middleware.

```
GET  /api/accreditation/members/{MemberNumber}          AccreditationAccountDTO list (0..n)
POST /api/accreditation/members/{MemberNumber}/eligible  sets ekklesia_eligible=true; 404 none, 409 ambiguous
POST /api/accreditation/temporary-accounts/check         { same body as create } -> TemporaryAccountCheckDTO (Ok, Problems per field, SuggestedUsername, ExistingAccounts)
GET  /api/accreditation/config                           UsernameSeparator, KeycloakConfigured
POST /api/accreditation/temporary-accounts               { FirstName, LastName, MemberNumber, Username?, Email?, Eligible }
                                                        -> { Username, Password, KeycloakUserId } | 400 | 409 existing account
```

Member numbers are validated as positive integers. Keycloak failures return 502 with the German message.

## Configuration

```
keycloak.base_url             e.g. https://sso.piratenpartei.de
keycloak.auth_realm           default master
keycloak.realm                target realm
keycloak.client_id            client-credentials client with manage-users
keycloak.client_secret        secret
keycloak.username_separator   default "."
```

The page shows "Keycloak ist nicht konfiguriert." when base URL, realm, client id or secret is missing.

## Permission and Audit

`PermissionIdentifier.ManageAccreditation = "accreditation_manage"`, global, display name "Akkreditierung durchführen", not in any default role and not in `SystemPermissions`.

Audit log entries (entity type `KeycloakAccount`, entity id = the Keycloak user id parsed as a Guid, since Keycloak ids are UUIDs): eligibility set (old -> new value, member number), temporary account created (member number, username, eligible flag). Passwords never appear.

## Testing

- Unit: username rule (normalization, prohibited characters, hash suffix values identical to MemberDataImport for sample numbers, both-taken failure); password generator (format, word list size, no repeats in a sample of draws beyond chance); exact member-number filtering of search results.
- HTTP client: request shapes against a stub handler (token caching, `q` search, create body with credentials and attributes, PUT without username/email, 409 mapping).
- Endpoints with a fake `IKeycloakAdminClient`: 401/403 on every route; search 0/1/n; set eligibility on 1 match, 404, 409 ambiguous; create refused for an existing member number; suggested username suffixed when taken; manual username refused when taken or invalid; password present in the response and absent from audit log rows.
- Manual: one end-to-end run against the real Keycloak with a test member number, then delete the test account.

## Documentation

- `Configuration.md`: a "Keycloak (Akkreditierung)" section with the options and the required client role.
- `V3.md` (V3 is not deployed yet): short German changelog entry for the new page.
