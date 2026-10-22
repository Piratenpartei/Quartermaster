# Dunning (Mahnungen) (V9.0)

## Goal

Members who do not pay their dues can be struck from the membership (Finanzordnung § 7) after at least two Mahnungen at least 30 days apart, once they are more than 12 months in arrears. Today the Landesverbände do this by hand. Quartermaster keeps the open dues per period, sends the Mahnungen by email or letter, records each one as evidence for a possible case at the Föderales Schiedsgericht, and files the strike request as a Redmine ticket for the Bundesmitgliederverwaltung. The Streichung itself stays in Sage. The procedure follows `mahnung-streichung-verfahren.md` (decided 2026-10-21).

## Decisions

- Open dues are kept per period (year or payment interval) in their own table. A later importer fills it from Sage; until then periods are entered by hand. `Member.OpenFeeTotal` stays as a cross-check.
- "More than 12 months in arrears" means the oldest period with an open amount above 0 has a due date more than 12 months ago.
- The permission `dunning_manage` (chapter-scoped, part of the board role, assignable to any role) works on members of that chapter and its sub-chapters. In practice the Landesverbände send Mahnungen; Mahnungen of any chapter count.
- A member has at most one active dunning case at a time, whoever started it, so a LV and the BV cannot dun in parallel.
- Dunning starts by hand only: the officer selects members and creates their next Mahnung in a batch. Nothing is sent automatically.
- Channel: email if the member has an address, `PostBounce` is not set and no email Mahnung in this case has bounced; otherwise letter. All letters of a batch come as one PDF; who prints and posts them is up to the LV.
- An email Mahnung counts once sent and not bounced. A letter counts once marked as sent, even if it comes back. Bounces come from the existing DSN poller, from `PostBounce`, or are marked by hand. A returned letter gets a date and a note; no scans are stored, the LV keeps the paper.
- Payment deadline: 14 days for email and 16 days for letters after sending, both settable per chapter.
- After a partial payment the officer either continues the case (earlier Mahnungen keep counting, the current periods become the new reference) or starts over (the case ends, a new one starts from zero).
- The strike request is a Redmine ticket with all data in its text (no PDF, no attachments), created through the chapter's existing Redmine connection. Redmine is HTTPS-only and does not follow redirects; this stays so.
- The Streichung notice is a letter rendered from a template, sent by the LV.
- The old Sage rule "more than 3 months without any dues booking" is dropped; the payment check below covers its purpose.
- Released as V9.0 with migration `M009`. UI name "Mahnungen"; a dunning case is a "Mahnverfahren", a batch a "Mahnlauf". Code and routes stay English (`Dunning`).

## Data model

All tables are new.

- `MemberDuePeriods`: `Id`, `MemberId`, `Period` (label such as "2025" or "Q3/2025", max 50, unique per member), `DueDate` (date), `Amount` (> 0), `OpenAmount` (0 to `Amount`), `Source` (`Manual`, `Import`), `UpdatedAt`.
- `DunningCases`: `Id`, `MemberId`, `ChapterId` (the chapter that runs it), `Status` (`Open`, `Stopped`, `Review`, `Paid`, `Requested`, `Struck`, `Cancelled`), `StopReason` (enum, nullable: `ReductionRequested`, `PaymentAnnounced`, `Other`), `StopNote` (max 500, optional), `EndReason` (enum, nullable: `Paid`, `PartialPayment`, `Exited`, `Manual`), `RedmineIssueId` (nullable), `RequestReference` (Guid, nullable), `CreatedAt`, `CreatedByUserId`, `ClosedAt` (nullable).
- `DunningCaseReferencePeriods`: `CaseId`, `Period`, `OpenAmount`. The periods as they stood when the case started, or when it was continued after a partial payment.
- `DunningBatches`: `Id`, `ChapterId`, `Channel` (`Email`, `Letter`), `CreatedAt`, `CreatedByUserId`, `SentOn` (date; set at once for email, confirmed by hand for letters).
- `DunningNotices`: `Id`, `CaseId`, `BatchId`, `Stage` (`First`, `Final`), `Channel`, `Address` (email address or the multi-line postal address used), `Subject`, `Body` (rendered text as sent), `TemplateIdentifier`, `PaymentDeadline` (date), `OpenTotal`, `NotificationLogId` (email only), `Result` (`Pending`, `Delivered`, `Bounced`, `Returned`, `Failed`), `ResultOn` (date, nullable), `ResultNote` (max 500, optional), `CreatedAt`.
- `DunningNoticePeriods`: `NoticeId`, `Period`, `DueDate`, `Amount`, `OpenAmount`. The snapshot that went into the Mahnung.

The notice keeps its own copy of subject and body, so the evidence survives when the retention sweep clears `NotificationLogs.Body`.

## Rules

- A notice counts when it was sent (`SentOn` of its batch set) and its result is neither `Bounced` nor `Failed`. A letter with result `Returned` counts.
- The stage of the next notice is `First` with no counted notice and `Final` with one. A case with two counted notices gets no further notice.
- A next notice is blocked when the case is `Stopped`, `Review` or closed; when the last counted notice was sent less than 30 days ago; when a letter of the case is not yet marked as sent; or when the chosen channel has no address (no email and no postal address: "nicht erreichbar").
- A payment is detected when any reference period has a lower open amount now, or is gone. New periods with fresh dues do not hide a payment.
- Re-evaluation runs after every change to a member's periods (manual or import): nothing open ends the case as `Paid`; a detected payment on an `Open` or `Stopped` case sets `Review`. From `Review` the officer continues (reference reset to the current periods, back to `Open`) or starts over (case ends with `PartialPayment`, a new `Open` case with a new reference starts).
- A case is strikeable when all hold: status `Open`; more than 12 months in arrears; two counted notices, the second sent at least 30 days after the first and of stage `Final`; the payment deadline of the `Final` notice has passed; no payment detected. Following the CLAUDE.md rule, this is one method with guard clauses.
- A member who exits (import sets `ExitDate`) while the case is `Open`, `Stopped` or `Review` ends it as `Cancelled` with `Exited`. In status `Requested` the exit date sets `Struck` instead.
- Floating members (no member number), exited and anonymized members are never offered for dunning.

## Flow and UI

- Arrears list "Beitragsrückstände" (route `/Dunning`), per chapter in reach of the permission: member, member number, open total, oldest due date, months in arrears, case status, next channel; filters for months, amount and status; a hint where `OpenFeeTotal` and the sum of the periods differ. Members are selected here for a Mahnlauf.
- Member detail: a section "Beiträge und Mahnungen" with the periods (add, edit, delete by hand) and the case as a timeline of notices and status changes, with the actions Mahnstopp, Mahnstopp aufheben, Fortsetzen, Neu beginnen, Abbrechen, Gestrichen.
- Mahnlauf: a check page lists each selected member with stage, channel, deadline and the reason when blocked. Confirming creates the cases where needed, one email batch and one letter batch. Emails go out through the existing email queue with a `NotificationLog` (source `DunningNotice`). Letters download as one PDF (DIN 5008 letter per member, rendered on demand, never written to disk) and count from the date confirmed with "Versendet am". An unconfirmed letter batch can be discarded.
- Results: the DSN poller, after recording a bounce, marks the linked notice `Bounced`; the next notice of that case goes by letter. "Gebounced" and "Brief zurück" (date and note) can be set by hand.
- Strike request: a view of strikeable cases, select, "Antrag in Redmine anlegen". One ticket per request in the chapter's project for strike requests, with the assurance (two Mahnungen, more than 12 months in arrears) and a Textile table: member number, name, chapter, open total, oldest due date, date and channel of both counted notices. The description carries the request reference, so a retry after a timeout finds the ticket instead of creating a second one. The cases move to `Requested` with the ticket number.
- Streichung notice: for `Struck` cases a letter batch from the notice template, one PDF.

## Templates and settings

- Templates in the existing template system (per-chapter overrides): first Mahnung email and letter, final Mahnung email and letter (announcing the Streichung for the deadline), Streichung notice letter, Redmine ticket text. Variables: member, periods table, open total, payment deadline, payment details, Verwendungszweck, fee reduction link, contact address. The notice letter names the Föderales Schiedsgericht, not the LV Schiedsgericht. Wording is drafted with the feature and finalised by the user.
- Chapter options (resolved up the chapter tree), in a new settings card "Mahnungen": `dunning.deadline_days.email` (14), `dunning.deadline_days.letter` (16), `dunning.bank.holder`, `dunning.bank.iban`, `dunning.bank.bic`, `dunning.reference_pattern` (with the member number placeholder), `dunning.reduction_form_url`, `dunning.contact_email`, `dunning.letter.sender_line`, `dunning.redmine.project_id`, `dunning.redmine.tracker_id`. The settings catalog test gets these entries.

## Data interface for the importer

One row per member and period: member number, period, due date, amount, open amount. The importer replaces the periods of each member in the file (source `Import`) and then runs the same re-evaluation as a manual change. No booking date is needed. The format is settled once the user knows what Sage can export.

## Security and audit

- Every endpoint checks `dunning_manage` on the member's chapter; a member outside the officer's reach answers 404.
- The audit log records period changes, case start and status changes, Mahnstopp, notices created, results set, batches confirmed, and strike requests.
- Batches of member data leave Quartermaster only through Redmine (HTTPS-only). Letter PDFs are only served over the app itself.
- How long cases and notices are kept after they end goes to the "Review data retention" todo; an FSG objection can come years after the Streichung.

## Testing

- Unit: counting rules, stage and channel choice, blocking reasons, payment detection with new periods, strikeable check (each guard), re-evaluation transitions.
- Integration: period entry and validation, Mahnlauf with mixed channels, letter batch confirmation and the 30-day spacing from `SentOn`, DSN bounce linked to its notice, strike request against a fake Redmine client including the retry by reference, import exit date to `Struck` and `Cancelled`, foreign chapter gets 404.
- No contact with a real Redmine or mail server from tests.

## Out of scope

- The importer for due periods (after the data question is answered).
- Automatic sending, scans of returned letters, Redmine attachments, booking dates.
- The retention period for dunning data.
