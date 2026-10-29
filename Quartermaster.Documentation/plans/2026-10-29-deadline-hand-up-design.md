# Hand undecided applications up after a deadline (V9.0)

## Goal

§ 3 Abs. 2b BS: when the chapter responsible gives no answer on a membership application within 4 weeks, the Landesvorstand may decide on the admission itself; after 2 months without an answer, the Bundesvorstand may. Quartermaster makes this a hard rule: an application motion that is still undecided when the deadline of a higher chapter passes moves there automatically, so applications do not go stale. Reduced due selections have no deadline; the Bundesvorstand can take them over by hand.

## Chapter level

A new technical level on every chapter, independent of its name (a "Stadtverband" may be a Kreisverband or an Ortsverband, a "Regionalverband" a Kreisverband):

- Enum `ChapterLevel` (append only): `Federal` = 0 "Bundesverband", `State` = 1 "Landesverband", `District` = 2 "Bezirksverband", `County` = 3 "Kreisverband", `Local` = 4 "Ortsverband".
- `Chapters.Level` (nullable; shown as "nicht gesetzt"). `Federal` is reserved for the root and the root is always `Federal`: its dropdown is read-only and disabled, and the server refuses `Federal` on any other chapter.
- Set with a dropdown on the chapter edit page; shown on the chapter detail page. For now it is only used for the default periods; routing keeps using the chapter tree.
- M009 fills it once: the root gets `Federal`, its direct children `State`; all other chapters stay unset.

## Take-over period

- `Chapters.TakeOverPeriodDays` (nullable int, 1 to 365): how many days the chapters below it have before this chapter takes over. Empty means the default for its level: `State` 28 days, `Federal` 62 days (never shorter than two calendar months, so the federal chapter never takes over too early). Other levels have no default.
- A chapter takes over only when it has an effective period (its own or its level's default). So a Bezirksverband is passed over unless a period is set on it, and the statutes' step from the chapter responsible straight to the LV holds.
- There is no switch to turn taking over off; a chapter can only change the number of days.
- Edited on the chapter edit page next to the level, with the effective default shown as the placeholder. Periods and levels are read live, so a change (for example after a statute change) applies at once to applications already waiting.

## Deadlines

For an application motion waiting at chapter C:

- The clock starts at the application's `SubmittedAt`, including the time it waited for its division to be linked. Every take-over counts from the same start: the LV's deadline is start + its period, the federal chapter's is start + its period.
- Candidates are the chapters strictly above C, nearest first, that have an effective period and handle applications: not marked for member administration (`MarkFor(MemberAdministration) == null`) and, with application forwarding enabled, with the application mode Quartermaster. The root is always a candidate (its period applies even with forwarding to a Redmine), like the fallback of the mark handover.
- Due: the highest candidate whose deadline has passed takes the motion (when settings changed so that several have passed, the motion goes straight to the highest). Example: when the LV handles applications through a Redmine, the Kreisverband's motion stays until the federal deadline and then goes to the federal chapter.
- Next move (for the info box): the candidate with the earliest deadline still ahead.
- Only motions linked to an application (`LinkedMembershipApplicationId`), in status `Pending`, whose application is `Pending`, count. Votes cast so far do not stop the move; only a finished decision does. Motions linked only to a due selection, the federal approval motion of the register and applications without a chapter are never moved by the deadline.

One pure helper `TakeOverSchedule` computes the due and the next candidate from the ancestor chain, the motion's chapter, the start, the clock and the forwarding facts, so it can be tested without a database.

## Sweep

`TakeOverHostedService` runs every 15 minutes, in the pattern of `AssemblyMotionDeadlineHostedService` (per-iteration errors are logged, the service keeps running). Per due motion, in its own transaction, it hands the motion over to the due candidate through `MotionHandover`. After the commit the target chapter's officers get the usual new-application notification; the lower chapter is not notified. A motion decided meanwhile is skipped by the existing status-conditional close.

## Handover reasons

`MotionHandover` gets a public entry for a single motion with a target chapter and a reason; the mark handover goes through it as well. It does what the mark handover does today (close without action, fresh copy with the same links and `OnBehalfOf*`, linked record and forwarding cases follow, audit entry on the old motion, no Redmine ticket).

- New column `Motions.HandoverReason` (nullable enum `MotionHandoverReason`, append only: `ChapterMark` = 0, `Deadline` = 1, `TakenOver` = 2), set on the closed motion together with `HandedOverToMotionId`.
- A copy keeps the `OnBehalfOf*` of the motion it replaces; a deadline or take-over move adds none.
- The new motion finds its predecessor through `HandedOverToMotionId` (no extra column) for its notice.
- Votes on a handed-over motion are refused for every reason (400, `error.motion.handed_over`), and the vote buttons are hidden: the decision now lies with the other chapter, so a symbolic vote there would only mislead.

## Take-over of reduced due selections

- `POST /api/motions/{Id}/take-over`: a `Pending` motion linked to a due selection (a standalone reduced due selection, or the reduction inside an application) at a chapter other than the root moves to the root with reason `TakenOver`. 404 for an unknown motion, 400 for one that is not such a motion or no longer pending, 403 without "Beitragsauswahlen bearbeiten" (`dueselections_process`) on the root or globally.
- The button "Übernehmen" sits on the motion detail page for those holders, behind a confirmation dialog. The lower chapter is not notified; the closed motion carries the mark that it was taken over and can no longer be voted on.

## UI texts

- Closed motion, reason `Deadline`: "Nach Fristablauf an Y übergeben", with the link to the new motion.
- Closed motion, reason `TakenOver`: "Vom Bundesverband übernommen; hier kann nicht mehr abgestimmt werden.", with the link.
- Closed motion, reason `ChapterMark`: unchanged ("An Y übergeben").
- New motion after a deadline move: "Von X nach Fristablauf übernommen (eingegangen am TT.MM.JJJJ)."
- Info box on a waiting application motion and on the application detail page: "Geht am TT.MM.JJJJ an Y, falls bis dahin nicht entschieden."
- The fields behind these texts are left out of motion DTOs for anonymous callers, as with the mark fields.

## Documentation

Changelog V9.0 (section "Mitgliedsantrag") gets a short paragraph on the deadlines, the chapter level and the take-over of reductions. PiratenWissen (`quartermaster/mitgliedschaft.md`, the page on the membership flow) gets a section on the deadlines and what moves when, with the source line "Quelle: Bundes-IT (Quartermaster Version 9.0), notiert 2026-10-29", and a `chronik.md` entry.

## Testing

- `TakeOverSchedule`: KV under LV (moves to the LV at 28 days, to the federal chapter at 62), Bezirksverband passed over without a period and taking over with one, a marked LV and a Redmine LV passed over, the root as a candidate with forwarding to a Redmine, several deadlines passed at once, overrides, the next move for the info box, a motion at the root.
- Sweep: a due motion handed over with links, forwarding case and notification of the target only; a motion decided meanwhile skipped; reduction motions, the federal approval motion and non-pending applications untouched.
- Level and period: `Federal` refused off the root, the root fixed to `Federal`, period bounds, the M009 fill.
- Take-over endpoint: status order (404, 400, 403), root refused, an application motion refused, permission on the root or global only.
- Votes refused on handed-over motions; mark handover unchanged apart from the reason.

## Decisions

- Five technical levels; names differ, the level is what counts.
- The root is always `Federal`; M009 prefills the root and its children.
- Periods in days, defaults 28 and 62, overridable per chapter, never switched off.
- The clock starts at submission; all deadlines count from the same start.
- Marked chapters and, with forwarding, Redmine chapters are passed over; the root always counts.
- Periods and levels are read live.
- The lower chapter is not notified, neither on a deadline move nor on a take-over.
- Votes on handed-over motions are refused.
