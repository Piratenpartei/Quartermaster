# Redmine forwarding for membership applications and due selections (V7.0)

## Goal

Quartermaster becomes the intake for membership applications and due selections of the whole party, while chapters keep their own workflows. Every submission is documented by a ticket in the federal Redmine. Decisions are made either in Quartermaster (board motion) or in a chapter's own Redmine, depending on per-chapter settings. Chapters that set up nothing receive their cases through the federal Redmine as before. Ships as V7.0 with migration `M007`; V6.1 (forum mirror) may be deployed before or together with it.

## Decisions

- Due selection is login-only; submissions take effect without email confirmation.
- A reduction still needs a decision; 72 EUR and above needs none.
- Thresholds are configurable: 72 EUR (regular minimum) and 12 EUR (below it the federal chapter decides).
- Due selections are decided at the state chapter (directly below the federal chapter); applications at the nearest chapter with a setting.
- Tickets are created by a background job with few, short retries; then a failure email and a list with retry and manual resolution.
- Settings per chapter, Redmine connection per chapter with inheritance up the tree; ticket content from templates plus a custom field mapping.
- Data of submissions handled in Redmine is only kept until their tickets exist (data minimisation); afterwards a log without personal data remains.
- A decision made in Quartermaster is written back as a note onto the federal ticket, optionally changing status and assignee.
- An application and its reduction are routed separately.
- The server determines an application's chapter itself.
- With a linked member record, the due selection's person data is locked; otherwise prefilled and editable.
- A motion set to "formally rejected" keeps sending no decision email; chapter officers inform the person themselves.

## Chapters involved

- F: the federal chapter, the root chapter (`ChapterRepository.GetRootChapter`).
- S: the state chapter of a member: the ancestor of `Member.ChapterId` directly below F. Absent when the member has no chapter, belongs to F directly, or no member record is linked.
- A: an application's chapter, determined by the server from the administrative division with the logic of `ChapterRepository.FindForDivision`. The client's `ChapterId` is ignored. When no chapter is found, the existing "PendingDivisionLinking" state applies.
- H: the handling chapter of an application: the nearest chapter from A upwards, excluding F, with an application setting.

Lower-chapter settings are resolved only up to, not including, F. F's own settings are separate rows and never act as a default for lower chapters.

## Routing

Every chapter except F has two settings, each `Quartermaster`, `Redmine` or unset: the application setting and the reduction setting (only read for state chapters). F has the same two settings.

Due selections (standalone, and the dues part of an application):

1. Amount at or above `dues.regular_minimum` (72): a ticket in F's "regular" target. No decision, nothing stored in Quartermaster. For the dues part of an application this step is skipped; the dues are part of the application ticket.
2. Amount below `dues.regular_minimum`: first a ticket in F's "reduction" target. The deciding chapter is S when the amount is at or above `dues.state_decision_minimum` (12) and S exists, otherwise F. By the deciding chapter's reduction setting:
   - Quartermaster: a due selection record and a review motion in the deciding chapter; after the decision a note on the federal ticket.
   - Redmine: at S a ticket in S's Redmine that links the federal ticket; at F the federal ticket itself is the decision ticket and nothing follows.
   - Unset: nothing further; the case is passed down inside Redmine.

Membership applications (anonymous with email confirmation as today, applicants have no SSO account):

1. Always a ticket in F's "application" target first.
2. H with application setting Quartermaster: the application record and its review motion in H as today; after the decision a note on the federal ticket.
3. H with application setting Redmine: a ticket in H's Redmine that links the federal ticket.
4. No H and A is F (for example abroad): F's application setting decides; Quartermaster means a motion in F plus a note, Redmine means the federal ticket is the decision ticket.
5. No H and A is a lower chapter: nothing further; passed down inside Redmine.
6. A reduction in the application is routed as a separate due selection case (above). The two cases reference each other; the application motion no longer covers the reduction.

Master switches `forwarding.membership_application.enabled` and `forwarding.due_selection.enabled` (default off): while off, no tickets are created and every chapter counts as `Quartermaster`. An application is then handled in A itself (H = A, as today), and a due selection at or above `dues.regular_minimum` is stored as `AutoApproved` (as today) instead of being forwarded. The thresholds, the separate reduction routing, the login requirement and the server-side chapter still apply.

Outcome note: when a motion of a Quartermaster-path case is decided (vote, status change including "formally rejected", meeting), the case's waiting note step becomes due. It adds a note to the federal ticket with result, deciding body and date, and optionally sets the target's note status and note assignee.

## Settings

Tables:

- `RedmineConnections`: ChapterId (unique), BaseUrl, ApiKey (secret: write-only, never returned, never logged), UpdatedAt. The row of F is the federal instance.
- `ForwardingTargets`: ChapterId, Purpose (`Regular`, `Reduction`, `Application`), ProjectId, TrackerId, PriorityId (optional), AssigneeId (optional), CustomFieldsJson (list of field id and Fluid snippet), NoteStatusId (optional), NoteAssigneeId (optional). Unique per chapter and purpose.
- `ForwardingChapterSettings`: ChapterId (unique), ApplicationMode, ReductionMode (each `Quartermaster`, `Redmine` or null), FailureEmail (optional).

Options (global): `dues.regular_minimum` (72), `dues.state_decision_minimum` (12), `forwarding.membership_application.enabled`, `forwarding.due_selection.enabled`.

Resolution:

- Redmine connection for a chapter: its own row, otherwise the nearest ancestor's, F included.
- Target: only the chapter's own row.
- Failure address: the nearest one up the tree, F included.

All settings require the global `EditOptions` permission.

## Assistants

- "Redmine" (`/Administration/Setup/Redmine`, card in the Integrations group): federal instance (URL, API key, "Verbindung prüfen" via `GET /users/current.json` showing the account name); chapter overrides via the chapter picker with their own URL and key, check and removal.
- "Weiterleitung von Anträgen" (`/Administration/Setup/Forwarding`): master switches and thresholds; federal section with the three targets, F's two settings and the failure address; chapter overrides via the chapter picker with application setting, reduction setting (state chapters only), the targets needed by a "Redmine" setting, and the failure address. Project, tracker, priority and status are dropdowns loaded through the chapter's resolved connection; assignee and custom field ids are numbers. Saving is refused when a "Redmine" setting has no resolvable connection or an incomplete target. Links to the templates.

## Data model and job

`ForwardingCases`: Id, Kind (`Application`, `DueSelection`), chapter ids (A, H or deciding chapter, S), RelatedCaseId, MembershipApplicationId, DueSelectionId, MotionId (Quartermaster path), PayloadJson (snapshot of the submitted data; the only copy of personal data on the Redmine path), Status (`Open`, `Done`, `Failed`, `ManuallyResolved`), CreatedAt, CompletedAt.

`ForwardingSteps`: Id, CaseId, Order, Kind (`CreateTicket`, `AddOutcomeNote`), TargetChapterId, Purpose, State (`Waiting`, `Pending`, `InFlight`, `Done`, `Failed`), TicketId, TicketUrl, Attempts, NextAttemptAt, LastError.

Job: a hosted service runs every minute and processes due steps per case in order. A chapter ticket step runs only after the federal step of its case has a ticket id. Templates are rendered when a step runs. Retries: three attempts (immediately, after 5 minutes, after 15 minutes); a rejected key (401/403) and a refused ticket (422) fail immediately.

Duplicate protection: each description ends with `Quartermaster-Vorgang QM-<short id>`. A step is set to `InFlight` before the create request; a step found `InFlight` first searches the target project (`GET /issues.json?project_id=..&description=~QM-<short id>&status_id=*`) and adopts a found ticket.

Failure: a failed step sends the failure email (template `forwarding.failure.email`, by default with the rendered ticket) to the failure address of the step's target chapter and the case shows up in `/Administration/Forwarding` with "Erneut versuchen" (resets attempts) and "Manuell erledigt". Visible for the global `ProcessApplications` permission (applications) and the global `ProcessDueSelections` permission (due selections).

Retention: PayloadJson is cleared when all ticket steps are done, and after "Erneut versuchen" succeeds or "Manuell erledigt". The note step needs only the federal ticket id and the outcome from the Quartermaster records. The remaining log holds kind, chapters, ticket ids and dates, no personal data.

## Templates and custom fields

Existing template system (rows in `Templates`, per-chapter overrides resolved up the tree, mock data preview). Subject is the ticket subject, body the description. Identifiers: `forwarding.redmine.application`, `forwarding.redmine.due_selection`, `forwarding.redmine.reduction`, `forwarding.redmine.outcome_note` (body only), `forwarding.failure.email`. A chapter ticket resolves from its target chapter, a federal ticket from F.

Variables: `application.*` (name, address, date of birth, email, phone, assigned chapter, payment data), `dues.*` (income group, amount, reduced, reduced amount, justification), `member.*` (member number, name, email), `submission.*` (submitted at; for due selections the submitting account and whether data was entered by hand), `chapter.*` (deciding or handling chapter, state chapter), `case.Reference`, `federalTicket.Id`, `federalTicket.Url` (chapter tickets), `outcome.*` (note: result, deciding body, date, motion link). Default templates contain what officers see in Quartermaster today, including the IBAN.

Custom fields: each entry is a Redmine custom field id and a Fluid snippet rendered with the same variables; an empty result leaves the field out.

## Redmine client

`IRedmineClient` / `HttpRedmineClient`: JSON API, header `X-Redmine-API-Key`, https only (http only for localhost, 127.0.0.1, [::1]), no redirects, 30 s timeout, response bodies never logged, the key never in errors. Calls: current user, projects, trackers, issue statuses, issue priorities, create issue (`POST /issues.json`), update issue with notes, status and assignee (`PUT /issues/{id}.json`), search by reference. The federal ticket is linked in chapter tickets as `{base URL}/issues/{id}` through the template; no Redmine relation.

## Changes in Quartermaster

- Due selection: `POST /api/dueselector` requires login; the anonymous pending path for due selections is removed; the Blazor form asks to log in. With a linked member the server takes member number, name and email from the member record and the form shows them readonly and disabled; without one the account's data is prefilled and editable and the case records "entered by hand". Submissions take effect immediately.
- Applications: the server determines the chapter from the division and ignores the client's `ChapterId`.
- `DueSelection` gets `ChapterId` (deciding chapter), so state boards see their reductions in their own list.
- A standalone reduction on the Quartermaster path creates a review motion in the deciding chapter linked to the due selection; the existing cascade and decision email apply.
- Motion resolution (vote, status change, meeting) activates the waiting note step of the linked case.
- The applicant's receipt email is always sent; decision emails on the Quartermaster path as today.

## Tests

No test or dev run contacts a real Redmine; `FakeRedmineClient` and a recording HTTP handler replace it.

- Routing as a pure function over every case above (thresholds, with and without S, each setting at S, H and F, abroad, application with reduction, switches off).
- Redmine client: headers, bodies, status code translation, no body or key in logs.
- Job: federal before chapter with the link, three attempts with 5 and 15 minute delays, immediate failure on 401/403/422, failure email to the resolved address, adoption via reference search after `InFlight`, payload cleared after completion, retry and manual resolution, note step only after the decision with status and assignee.
- Endpoints: due selection 401 anonymous; locked fields with a member record, editable without; server-side application chapter despite a different client value; separate reduction motion in the deciding chapter; failure list and actions only with the matching global permission; assistant endpoints only with `EditOptions`; API key never returned.
- Templates: each new template renders with mock data; defaults contain all listed fields.

## Docs

- `Configuration.md`: Redmine connection and overrides, routing with examples, forwarding assistant, templates and variables, failure list, first run checklist with a test project.
- `Translations.md`: Weiterleitung = forwarding, Vorgang = case, Zielprojekt = target project, and the setting names.
- `Changelog/V7.0.md`.
