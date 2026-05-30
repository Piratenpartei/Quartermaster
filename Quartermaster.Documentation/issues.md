# Known issues / follow-ups

## Dispatchers bind anonymous-typed subsets, not the full DTOs

The template field palette in `/Administration/Templates/{id}` reflects fields off DTO
types (e.g. `MembershipApplicationDetailDTO`, `MotionDetailDTO`,
`MemberDetailDTO`). However, the actual model dictionaries passed into the Fluid
renderer at dispatch time are anonymous types holding only a hand-picked subset
of properties:

- `Quartermaster.Server.Email.EmailService.EnqueueEmailAsync` — binds only
  `{ FirstName, LastName, Email, MemberNumber, City, PostCode }` for `member`;
  injects no `event`, `chapter`, or `globals`.
- `Quartermaster.Server.MembershipApplications.MembershipApplicationMailService`
  — anonymous `application` with 5 fields; anonymous `chapter` with `Name`.
- `Quartermaster.Server.DueSelector.DueSelectionMailService` — anonymous
  `selection` with 6 fields; anonymous `chapter` with `Name`.
- `Quartermaster.Server.Submissions.SubmissionConfirmationEmailService` —
  per-kind anonymous models.
- `Quartermaster.Server.MembershipApplications.ApplicationReviewService` and
  `Quartermaster.Server.Submissions.SubmissionMaterializer` — same pattern for
  notification triggers.

Consequence: the palette can advertise a field (e.g. `{{ application.City }}`)
that the dispatcher does not bind, so the rendered template gets an empty value
at runtime.

It also seems architecturally off that `EmailService` is the component
populating template models in the first place — template variable expansion is
a rendering concern, not a transport concern. A cleaner split would let each
trigger declare its full model (typed DTO instance) once, and have a single
renderer apply it regardless of channel.

**Action when picked up:** decide on the canonical model shape per
trigger/template, push full DTOs (or a typed model record) through to the
renderer, and remove the per-service anonymous projections. After that, the
palette and the runtime will agree on the available field set.
