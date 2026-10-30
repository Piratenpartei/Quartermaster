# Email code in the membership application (V9.0)

## Goal

An applicant who mistypes their email address today only notices when the confirmation link never arrives, and the application is lost. A 6-digit code sent to the address in its own wizard step, just before the summary, shows the address again, lets the applicant correct it on the spot and proves the address works before the application is submitted. For membership applications the code replaces the confirmation link: with a checked code the application is stored directly on submit. Board motions (`/api/motions`, anonymous) keep the link; the dues form and the assembly motion portal need a login and never had one.

## Who gets the step

- Anonymous applicants get the new step "E-Mail bestätigen" between "Erklärungen" and "Zusammenfassung".
- Logged-in users (officers entering an application for someone else) do not get the step and the server does not ask them for a code, as with the link today.
- The debug build without a configured mail server skips the code like it skips the link today (`SmtpConfig.ReadFrom(...) == null`), so the dev system works without mail. Real tests of the code mail happen on the staging machine.

## Code store

A new table `EmailVerificationCodes`, folded into M009, one row per address:

- `Email` (unique; trimmed and lower-cased before storing and comparing)
- `CodeHash` and `CodeSalt`: SHA-256 over salt and code; the code itself is never stored
- `ExpiresAt`, `FailedAttempts`, `VerifiedAt` (nullable)
- `SendCount` and `SendWindowStartedAt` for the per-address send limit

Requesting a code replaces the row's code, resets `FailedAttempts` and `VerifiedAt` and sets a new `ExpiresAt`. Codes are 6 random digits from a cryptographic random source, compared in constant time. The existing `PendingSubmissionCleanupHostedService` also deletes rows that expired more than a day ago.

## Sending

`POST /api/membershipapplications/email-code` with the address, anonymous:

- Refused like the submit when the application form is not set up or switched off.
- The address must pass `EmailAddressFormat.IsPlausible`.
- Rate limits, both adjustable in the settings and both 10 per hour by default: per IP (a new rate limit policy next to `AnonymousCreateRateLimitPolicy`, options `auth.ratelimit.email_code_permits` and `auth.ratelimit.email_code_window_minutes`) and per address (counted in the row, option `membership_application.email_code.sends_per_address_per_hour`). Over a limit the endpoint answers 429.
- Sends the new system template `templates.submission.membershipapplication.code.email` with `{{ code }}` and `{{ minutes }}` (the lifetime), resolved per chapter like the other templates:
  - Subject: "Bestätigungscode für deinen Mitgliedsantrag - Piratenpartei"
  - Body: "Dein Code lautet **{{ code }}**. Er ist {{ minutes }} Minuten gültig. Falls du keinen Mitgliedsantrag gestellt hast, ignoriere diese E-Mail."
- Lifetime: option `membership_application.email_code.lifetime_minutes`, default 15.

## Checking

One rule, used by a check endpoint and by the submit:

- No row for the address, or the code expired: refused with "abgelaufen" (no attempt counted).
- 5 failed attempts reached: refused with "zu viele Fehlversuche", a new code is needed.
- Wrong code: `FailedAttempts` + 1, refused with "falsch".
- Right code: `VerifiedAt` set and `ExpiresAt` moved to now + lifetime, so an applicant reading the summary has the full lifetime again before submitting.

`POST /api/membershipapplications/email-code/check` with address and code, anonymous, applies the rule; the wizard calls it when the applicant clicks "Weiter" on the code step, so a wrong code shows up on that step.

The submit (`MembershipApplicationCreateEndpoint`) takes the code in a new `EmailVerificationCode` field of `MembershipApplicationDTO` (never stored with the application) and applies the same rule against exactly the application's address. On success the application is stored directly through `SubmissionMaterializer.MaterializeApplicationDirectAsync` (receipt mail, notifications and forwarding as today) and the row is deleted; a submit refused for another reason leaves the code usable. When the submit is refused because the code expired, the wizard goes back to the code step and sends a new code there.

## Wizard step

`MembershipEmailCodeStep`:

- Shows "Wir haben einen Code an **adresse** geschickt." The code is sent automatically when the step opens, unless a code for this address was sent less than 60 seconds ago in this wizard session, and again after the address is changed.
- "Adresse ändern" turns the address into an input on the same step; saving writes it to the entry state (`State.Email`, also shown on the personal data step and the summary) and sends a new code.
- "Neuen Code senden" is disabled for 60 seconds after each send, with the remaining seconds shown.
- One 6-digit input (digits only, `inputmode="numeric"`, `autocomplete="one-time-code"`). "Weiter" is only possible with 6 digits and a successful check.
- The step is complete (for the stepper) when the code was checked for the current address; changing the address on the personal data step makes it incomplete again.

After the submit the page shows the existing "Der Mitgliedsantrag wurde übermittelt ..." notice instead of "Fast geschafft!".

## Templates

- New system template for the code mail (above).
- The link template `templates.submission.membershipapplication.confirmation.email` is removed from the built-in defaults, since nothing sends it any more. Rows in existing databases stay (system base templates cannot be deleted in the editor).
- Links already mailed before the deploy keep working until they expire (48 hours); `SubmissionConfirmEndpoint` and the `MembershipApplication` kind of `PendingSubmission` stay for that.

## Documentation

Changelog V9.0 (section "Mitgliedsantrag") gets a short paragraph. PiratenWissen `quartermaster/mitgliedschaft.md` gets the code step in the description of the online application, with the source line "Quelle: Bundes-IT (Quartermaster Version 9.0), notiert 2026-10-29", and a `chronik.md` entry.

## Testing

- Code store: hash and salt, replace on resend, expiry, attempt cap, case-insensitive address.
- Send endpoint: form gates, implausible address, per-address limit (429), mail sent with the code and lifetime, template resolved per chapter.
- Check endpoint and submit: right, wrong (counted), expired, locked, code for a different address; a correct check extends the lifetime; submit without code refused for anonymous callers; logged-in submit without code still direct; row deleted after a stored application; a submit refused for another reason keeps the code.
- No `PendingSubmission` is created for an anonymous application any more.

## Decisions

- Own step before the summary; the summary stays as it is.
- The code is sent automatically on opening the step and after an address change; resend after 60 seconds.
- Checked on "Weiter" of the code step and again at submit; a correct check extends the lifetime.
- 10 codes per address and 10 per IP per hour, 15 minutes lifetime, 5 wrong attempts per code; all adjustable.
- The debug build without SMTP skips the code.
- The link template for applications leaves the built-in defaults.
