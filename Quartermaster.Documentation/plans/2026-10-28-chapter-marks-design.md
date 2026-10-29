# Chapter marks "Keine Mitgliederverwaltung" and "Handlungsunfähig" (V9.0)

## Goal

Some chapters do not do member administration themselves, and some cannot decide anything at all. Today Quartermaster routes applications, reduced due selections and board motions to such a chapter anyway, where nobody decides them. Two independent marks on a chapter make Quartermaster route past it to the nearest chapter above that can decide, and move what is already waiting there.

- "Keine Mitgliederverwaltung" (`NoMemberAdministration`): the chapter does not handle membership applications and reduced due selections, for whatever reason; it may still be able to act otherwise.
- "Handlungsunfähig" (`UnableToAct`): the chapter cannot decide anything. Every board motion of the chapter goes up, not only applications and due selections. It implies skipping the chapter for member administration as well.

The federal chapter (root) cannot be marked; there is a separate procedure when the federal board cannot act, and it is not handled in Quartermaster.

## Data and migration

Folded into the in-flight migration M009 (the dev DB is altered by hand):

- `Chapters.NoMemberAdministration` and `Chapters.UnableToAct`, both boolean, not null, default false.
- `Motions.OnBehalfOfChapterId` (Guid, nullable) and `Motions.OnBehalfOfReason` (enum `ChapterMark`: `NoMemberAdministration`, `UnableToAct`; nullable; appended to only): the chapter the motion was routed past, and why. Stored on the motion, so the notice stays correct after the mark is lifted.
- `Motions.HandedOverToMotionId` (Guid, nullable): set on a motion closed by a handover, pointing at the motion that replaced it.

## Deciding chapter

One helper, `ChapterMarkRouting`, finds the chapter that decides for a given chapter and purpose. It walks the ancestor chain upwards starting at the chapter itself and returns the first chapter that is not skipped; the root is never skipped and is the last stop.

- Purpose `MemberAdministration` (applications, reduced due selections): skips chapters with either mark.
- Purpose `Motion` (every other board motion): skips chapters marked `UnableToAct`.

It also returns the first skipped chapter and its mark (`UnableToAct` wins when both are set), which becomes `OnBehalfOfChapterId` and `OnBehalfOfReason`. A motion that already has an `OnBehalfOfChapterId` keeps it when it moves on, so the notice always names the chapter the motion was originally meant for.

## Routing new submissions

`ForwardingRouter` stays pure; the marks enter through `ForwardingChapterContext`, which `ForwardingSettingsResolver.Build` fills.

- **Applications without forwarding:** the motion and the record go to the deciding chapter of the application's chapter (purpose `MemberAdministration`) instead of the application's chapter itself.
- **Applications with forwarding:** `HandlingChapterFor` skips marked chapters and continues upwards to the next chapter with an application setting. When none is left below the root, the existing path without a handling chapter applies (federal ticket; a motion at the federal chapter when its application mode is Quartermaster).
- **Reduced due selections** (standalone and inside an application): a marked state chapter counts as absent, so the federal chapter decides, with and without forwarding, and no state ticket is created for it. The state chapter as the chapter of a due selection that needs no decision (`ctx.StateId ?? ctx.FederalId`) is unchanged, since nothing is decided there.
- **Linking a waiting application to its division** (`ForwardingIntake.LinkApplication`) goes through the same routing, so an application linked to a marked chapter goes up.
- **Board motions submitted to a chapter** (`MotionCreateEndpoint`, `SubmissionMaterializer`): when the chapter is marked `UnableToAct`, the motion is stored at the deciding chapter (purpose `Motion`) with `OnBehalfOfChapterId` and `OnBehalfOfReason` set. The public motion form keeps offering the marked chapter, so members do not need to know about the mark.
- Motions created for applications and reductions get `OnBehalfOf*` set whenever routing skipped a chapter.

The public membership application wizard keeps showing the chapter found from the address; the marks are not shown to anonymous users.

## Handing over waiting motions

Setting a mark moves the chapter's open (`Pending`) motions of the affected kinds; lifting a mark moves nothing back, only new submissions route normally again.

- `NoMemberAdministration`: motions linked to an application or a due selection.
- `UnableToAct`: all motions.

Target: the deciding chapter for the motion's purpose, starting above the marked chapter. With application forwarding enabled, a motion linked to an application goes to the nearest chapter above that is not skipped and has the application mode Quartermaster, else to the federal chapter. A handover never creates Redmine tickets: what Quartermaster already holds stays in Quartermaster. Anything already forwarded to a Redmine is not touched.

A shared `MotionHandover` service (reused later by the deadline hand-up) does, per motion, in one transaction with the mark change:

1. Closes the old motion with the status-conditional update to `ClosedWithoutAction` and sets `HandedOverToMotionId`. This is no decision: no decision mail, no forwarding outcome, no federal approval step, no register entry.
2. Creates a new motion at the target with the same title, text, author and links, and `OnBehalfOf*` as above. Votes start fresh: the vote count does not check who voted, so the old chapter's votes must not carry over.
3. Moves the linked application or due selection to the target chapter (`ChapterId`) and points its forwarding case (`MotionId`, `DecidingChapterId`) at the new motion, so the outcome note on the federal ticket still follows the vote.
4. Writes an audit entry on the old motion.

A motion whose status changed meanwhile (a race with a vote) is skipped. After the commit the target chapter's officers get the usual notification for a new motion, application or due selection; the mark itself sends no notification.

A motion on the agenda of an open meeting of the marked chapter is handed over like any other; its agenda item keeps pointing at the closed motion.

## Setting the marks

- New chapter-scoped permission "Handlungsfähigkeit verwalten" (`chapters_manage_capacity`), added to the default officer permissions. The check is on the parent chapter, with the permission held globally, on the parent or inherited from a chapter above it (direct grants and inheriting roles; `PermissionContext.HasOnChapterOrAncestors`), since `Has` only inherits view permissions. It covers the officers of every higher chapter, but never the chapter's own officers. Delegates of the direct parent hold it too, as their role has the officer rights without inheritance.
- `PUT /api/chapters/{Id}/marks` with both flags; 400 for the root, 404 for an unknown chapter, 403 without the permission. Returns the number of handed-over motions.
- `GET /api/chapters/{Id}/marks/preview` with the requested flags: the target chapter(s) and the number of open motions that would move, for the dialog.
- Setting and lifting each mark go to the audit log as field changes on the chapter.

## UI

- Chapter detail: a "Handlungsfähigkeit" card with two switches, shown to holders of the permission. Turning a mark on opens a confirmation dialog naming the target chapter and the number of open motions that move ("3 offene Anträge werden an den Landesverband X übergeben."). Turning one off asks for a simple confirmation.
- Badges "Keine Mitgliederverwaltung" and "Handlungsunfähig" in the chapter list, the tree and the detail, for logged-in users only: the flags are left out of the chapter DTOs for anonymous callers, as with `ContactEmail`, including the tree endpoints (`roots`, `children`).
- Motion detail and list: an info box for `OnBehalfOfReason`:
  - `UnableToAct`: "Wird im Namen von X entschieden; X ist handlungsunfähig."
  - `NoMemberAdministration`: "Zuständig wäre X (keine Mitgliederverwaltung)."
- A handed-over motion shows "An Y übergeben" with a link to the new motion.
- The `OnBehalfOf*` and handover fields are left out of motion DTOs for anonymous callers (public motions).

## Documentation

Changelog V9.0 (section "Gliederungen") and the PiratenWissen pages on chapters and board work get a short paragraph on both marks, who sets them and what moves.

## Testing

- `ChapterMarkRouting`: walk with no marks, one mark, consecutive marks, marks by purpose, root as last stop, the first skipped chapter and reason.
- `ForwardingRouter`/resolver: applications with and without forwarding past a marked chapter, marked handling chapter, marked state chapter for reductions.
- `MotionHandover`: old motion closed without side effects, new motion with links and `OnBehalfOf*`, record and forwarding case moved, a raced motion skipped, an existing `OnBehalfOf` kept.
- Endpoints: permission on the parent only (own officers refused, higher officers and global admins allowed), root refused, preview counts, audit entries, flags hidden from anonymous callers in list, detail, search and tree.

## Decisions

- Root cannot be marked.
- Lifting a mark moves nothing back.
- Both marks get a notice on the motion: "im Namen von" for `UnableToAct`, "Zuständig wäre" for `NoMemberAdministration`.
- Anything already forwarded to a Redmine stays untouched; a handover never creates tickets.
- Moving is close and recreate, so votes start fresh at the new chapter.
- The permission is named "Handlungsfähigkeit verwalten" and checked on the parent chapter.
- The dialog shows the target chapter and the number of motions, no list.
- The deadline hand-up is a separate spec and reuses `MotionHandover`.
