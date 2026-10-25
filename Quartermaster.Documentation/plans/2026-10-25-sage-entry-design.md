# Approval mail after the SAGE entry

## Goal

An approved membership application must be entered in SAGE before the new member hears from Quartermaster. Today the approval mail goes out the moment the application is approved. From now on the approval updates the application's federal Redmine ticket with a request to enter the member in SAGE, assigns it to the responsible group, and Quartermaster sends the approval mail only once the ticket reports the entry as done. The process follows `ablaeufe/mitgliederverwaltung.md` in PiratenWissen (step 5: SGM enters the member in Sage and gives the ticket back).

## Decisions

- Applications are only handled in Quartermaster when this workflow is set up. Without it the membership form stays closed and the server refuses applications, also those entered by hand by officers.
- Every application handled in Quartermaster already gets a federal Redmine ticket (the documentation ticket). The SAGE request is a note on that ticket.
- Applications decided in a chapter's own Redmine are not affected; Quartermaster sends no decision mail for them today and still does not.
- Rejection mails go out at once, as today. The rejection note keeps the configured note status and note assignee.
- The approval note leaves the ticket status alone and assigns the ticket to the configured SAGE group.
- The "done" signal is configurable as a ticket status, as re-assignment to the Quartermaster account, or both; with both configured, both must hold.
- The SAGE settings sit on the federal application target, next to the note status and note assignee.
- The ticket is checked every 15 minutes by default; the interval is a setting.
- The provisional member number is always "RM" followed by the federal ticket number.
- The partial due for the entry year is counted to the month from the requested entry month on: yearly due × (13 − entry month) / 12, rounded to the cent. The requested entry date counts, even if the decision comes later.
- An application with its own reduction request is held until the reduction is decided, so the note can name the amount that applies. A reduction decided outside Quartermaster (in a chapter Redmine) cannot be waited for; the note then lists both amounts.
- The member number is not read from the ticket; it arrives with the next member import.
- There is no alert for tickets left waiting and no button to confirm the SAGE entry by hand (may come later). Marking the case as resolved by hand ends it without the approval mail.
- Ships in V9.0 with migration `M009`.

## Setup and the form gate

New fields on `ForwardingTarget` (used on the federal chapter's `Application` target only):

- `SageAssigneeId` (int, nullable): the Redmine group (or user) the approval ticket is assigned to. Assigning to a group needs the Redmine setting "Allow issue assignment to groups"; the editor says so in its hint.
- `SageDoneStatusId` (int, nullable): the status that means "entered in SAGE".
- `SageAwaitReassignment` (bool, default false): the ticket must be assigned back to the account of the API key.

New option `forwarding.sage.check_minutes` (global, default 15, minimum 5), shown in the forwarding settings assistant.

The SAGE setup is complete when the federal chapter has a Redmine connection and an `Application` target, `SageAssigneeId` is set, and `SageDoneStatusId` is set or `SageAwaitReassignment` is on.

`PublicFormSettings.IsMembershipApplicationReady()` is true when application forwarding (`forwarding.membership_application.enabled`) is on and the SAGE setup is complete. Then:

- `IsMembershipApplicationEnabled()` (the public form, the client config, the navigation) is the public switch AND ready.
- `MembershipApplicationCreateEndpoint` refuses every submission while not ready, including hand entries by holders of `ProcessApplications`, with a new error saying that membership applications are not set up.
- The public forms settings page shows a warning under the membership switch while it is on but not ready, naming what is missing (forwarding switch, federal Redmine connection, federal application target, SAGE group, done signal), with a link to the forwarding setup.
- The forwarding target editor shows the three SAGE fields for the federal `Application` target in their own block "SAGE-Eintragung" with a hint. Group and status are picked from the Redmine lookups like the existing fields.

## Steps

New step kind `ForwardingStepKind.AwaitSageEntry = 2`, UI name "Wartet auf SAGE-Eintragung".

`ForwardingIntake.Steps()` adds it after the `AddOutcomeNote` step for `Application` cases with `NoteAfterDecision`, in state `Waiting`, targeting the federal chapter. This covers arrival and `LinkApplication` (an application that waited for its division). The step exists from intake on, so whether an approval mail is held never depends on settings read later.

Approval mail hold: `MotionResolutionDecisionMailer` and `MembershipApplicationProcessEndpoint` skip `SendApplicationDecisionAsync` for an approved application whose application case has an `AwaitSageEntry` step. A small service (`SageEntryGate.HoldsApprovalMail(applicationId)`) answers this for both callers. Rejections are never held.

### The outcome note on approval

When the application is approved, `ForwardingJob.AddNoteAsync` works as today with three changes:

1. If the case has a related reduction case whose due selection is still `Pending` in Quartermaster, the step goes back to `Waiting` without an attempt. `ForwardingOutcomeHook` releases it when that reduction is decided (motion or processing dialog), but only once the application itself is decided, since a note without a decision is skipped.
2. The note is the outcome note followed by a blank line and the new template `forwarding.redmine.sage_request`.
3. The update assigns `SageAssigneeId` and leaves the status unchanged. A missing SAGE setup fails the step at once (`NotConfigured`) and sends the failure mail; the approval mail stays held.

On rejection the note is the outcome note alone with `NoteStatusId`/`NoteAssigneeId`, as today, and the `AwaitSageEntry` step is skipped.

### Waiting for the entry

The `OutcomeHook` releases the `AwaitSageEntry` step together with the note (`Waiting` to `Pending`). The job processes it after the note is done:

- Decision not an approval: `Skipped`.
- Otherwise read the federal ticket (`GetIssueAsync`) and, if `SageAwaitReassignment` is on, the account of the API key (`GetCurrentUserAsync`).
- Done when the status matches `SageDoneStatusId` (if set) and the assignee is the API key's account (if required). Then the approval mail goes out through `SendApplicationDecisionAsync`, the step is `Done`, and the case finishes.
- Not done yet: the step stays `Pending` with `NextAttemptAt = now + check interval`; `Attempts` and `LastError` are reset, since a successful check is no error.
- Redmine errors go through `RecordFailureAsync` as for the other steps (three attempts, permanent codes fail at once, failure mail). A retry by hand resumes the checks.
- The mail is sent before the step is stored as `Done`; a crash in between can send it twice, which is preferred over not sending it.

The application record stays in Quartermaster throughout (it is handled there), so the mail and the note render from it after the case payload is cleared. The retention sweep runs on a ten-year clock and does not interfere.

## Redmine client

`IRedmineClient` gets two reads, both through the existing https-only path without redirects:

- `GetIssueAsync(settings, issueId)` returns status id and assignee id (`GET /issues/{id}.json`).
- `GetCurrentUserAsync(settings)` returns id and login (`GET /users/current.json`); `GetCurrentUserNameAsync` uses it.

The note renders the status name through `ListIssueStatusesAsync` and the account login through `GetCurrentUserAsync`, so the wording never goes stale when the settings change.

## Note template

New system template `forwarding.redmine.sage_request`, "Redmine-Notiz: Bitte um SAGE-Eintragung", no subject, no chapter fields, editable in the template editor. Its model has `application`, `dues` (the full due selection, as on the ticket), `case` and a new `sage` model:

- `ProvisionalMemberNumber`: "RM" + federal ticket number.
- `EntryDate`: requested entry date (dd.MM.yyyy).
- `EntryYear`, `EntryMonth` (German month name).
- `YearlyAmount`: the yearly due that applies: the approved reduced amount, the rejection default after a rejected reduction, otherwise the selected due.
- `PartialAmount`: the partial due for the entry year from `YearlyAmount`.
- `ReductionResult`: "genehmigt", "abgelehnt", "offen" (decided outside Quartermaster) or empty.
- `PartialAmountIfReduced`: only with `ReductionResult` "offen": the partial due from the requested reduced amount; `PartialAmount` then uses the rejection default.
- `DoneStatus`, `DoneAccount`: status name and account login of the configured signals, empty when not used.

Default body:

```
**Bitte um Eintragung in SAGE**

- Vorläufige Mitgliedsnummer: {{ sage.ProvisionalMemberNumber }}
- Name: {{ application.FirstName }} {{ application.LastName }}
- Gewünschtes Eintrittsdatum: {{ sage.EntryDate }}
{% if sage.PartialAmount != "" %}- Beitrag {{ sage.EntryYear }} anteilig ab {{ sage.EntryMonth }}: {% if sage.ReductionResult == "offen" %}{{ sage.PartialAmountIfReduced }} € bei Genehmigung der Minderung, sonst {{ sage.PartialAmount }} €{% else %}{{ sage.PartialAmount }} €{% endif %}
{% endif %}
{% if sage.YearlyAmount != "" %}**Beitrag**

- Einstufung: {{ dues.Valuation }}
- Jahresbeitrag: {{ sage.YearlyAmount }} €
{% if dues.Reduced %}- Minderung auf {{ dues.ReducedAmount }} € ({{ dues.ReducedTimeSpan }}): {{ sage.ReductionResult }}
{% endif %}- Zahlweise: {{ dues.PaymentSchedule }}
{% if dues.IsDirectDeposit %}- Lastschrift: {{ dues.AccountHolder }}, IBAN {{ dues.Iban }}
{% else %}- Zahlung per Überweisung
{% endif %}
{% endif %}Die übrigen Personendaten stehen in der Ticketbeschreibung. Sobald {% if sage.DoneStatus != "" %}das Ticket auf „{{ sage.DoneStatus }}“ steht{% if sage.DoneAccount != "" %} und {% endif %}{% endif %}{% if sage.DoneAccount != "" %}das Ticket wieder dem Konto {{ sage.DoneAccount }} zugewiesen ist{% endif %}, verschickt Quartermaster die Aufnahmebestätigung.
```

`TemplateMockDataProvider` gets sample data for the `sage` model.

The failure mail (`forwarding.failure.email`) says a ticket "konnte nicht angelegt werden", which is wrong for note and waiting steps. It gets a `failure.Action` field ("angelegt", "um die Entscheidung ergänzt", "auf die SAGE-Eintragung geprüft") and a `failure.Remedy` field with the advice for that step kind: for a ticket, create it by hand and mark the case as done, or fix the cause and retry; for a note or check, fix the cause and retry, and for applications the warning that resolving by hand sends no confirmation. Both are updated in `M009` by REPLACE like the other templates.

## Display

- Forwarding cases: the waiting step shows its kind and, while waiting, the time of the next check.
- Application detail (officers): for an approved application with a held mail, an info line "Angenommen. Die Aufnahmebestätigung wird nach der SAGE-Eintragung verschickt." with a link to the federal ticket; once sent, "Aufnahmebestätigung verschickt am {date}". While the reduction is still open: "Wartet auf die Entscheidung über die Beitragsminderung." A failed note or check shows "Angenommen. Die Weiterleitung ist fehlgeschlagen, die Aufnahmebestätigung wartet." with a link to the forwarding cases; a case resolved by hand shows that the confirmation is not sent automatically, and the resolve confirmation of an application case warns about it. The detail DTO gets `ApprovalMailState` (`None`, `WaitingForReduction`, `WaitingForSage`, `Sent`, `Failed`, `ManuallyResolved`) and `ApprovalMailSentAt`, computed from the case steps.

## Migration (M009)

Folded into the in-flight `M009` (`[Migration(017)]`): the three `ForwardingTargets` columns and the failure mail REPLACE. The dev database gets the columns by hand. The option comes from `SupplementDefaults`, the new template from `SystemTemplateSeeds`. `SettingsAssistantCatalog` lists the option in the forwarding assistant.

## Tests

- `ForwardingRouter`/`ForwardingIntake`: the waiting step is queued for Quartermaster-handled applications, on arrival and after `LinkApplication`, and not for Redmine-handled ones.
- `PublicFormSettings`/`MembershipApplicationCreateEndpoint`/`ClientConfigEndpoint`: the gate for each missing part, including hand entries.
- `ForwardingJob` with a fake `IRedmineClient`: approval writes outcome plus SAGE note and assigns the group without a status; the waiting step checks, reschedules without counting an attempt, sends the mail on status only, re-assignment only, and both (each alone not enough when both are set); rejection skips the step; Redmine errors retry and fail with the mail; a missing setup fails the note.
- Reduction hold: the note waits for a pending reduction and is released by its decision; a reduction decided outside Quartermaster gives both amounts.
- `SageEntryGate` in the motion path and the processing dialog: approval held, rejection sent.
- Partial due: entry in January (full year), October (3/12), December (1/12), rounding.
- `SystemTemplateSeeds` matches the migrated failure mail body.
- `HttpRedmineClient`: the two reads against a local fake server, https enforced.
