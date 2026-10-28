# Register of rejected applicants (V9.0)

## Goal

§ 3 Abs. 2 Bundessatzung: applications from people who were expelled, who left while expulsion proceedings against them were running, or whose earlier application was rejected additionally need the approval of the Bundesvorstand, which should hear the chapter responsible. Quartermaster keeps a register of these people, checks every new application against it, and routes a hit through a second approval by the federal board.

Today a rejection is only `ApplicationStatus.Rejected` on the application itself, and `RetentionAnonymizationService` anonymizes it after 10 years, so the register keeps its own copy of the data.

## Register entries

New table `ApplicantRegisterEntries`, one row per known case:

- `Kind`: `Rejected`, `Expelled`, `LeftDuringExpulsion` (enum, appended to only).
- Person data used for matching: `FirstName`, `LastName` (both required), `DateOfBirth`, `Street`, `HouseNumber`, `PostCode`, `City`, `Email` (optional, but at least one of date of birth, street, post code and city, or email is required, since the names alone are only two fields and could never make a hit).
- `OccurredOn` (date of the rejection, expulsion or exit), `Note` (internal text), `ChapterId` (the chapter that rejected, when known), `SourceApplicationId` (the rejected application, when the entry came from Quartermaster), `CreatedAt`, `CreatedByUserId` (null for automatic entries).

The retention sweep does not touch the table; how long entries stay is decided later. Every create, edit and delete goes to the audit log.

Automatic entries: when an application is rejected in Quartermaster by an explicit decision against the person, by the chapter or by the Bundesvorstand, an entry of kind `Rejected` is created from the application's data. That is a motion resolved as `Rejected` or a rejection in the processing dialog. A formal rejection (`FormallyRejected`, e.g. when the applicant did not answer a question) creates no entry: such a person may still want to join and was only unable to react. Rejections decided in Redmine are not seen by Quartermaster and are entered by hand.

Manual entries: a new page "Ablehnungsregister" under "Verwaltung" lists the entries (search by name), and a simple form adds, edits and deletes them. This is also how the few known earlier cases and all expulsions get in; no import.

## Matching

Runs once per application, in `ForwardingIntake.RouteApplication` before the application is routed. Six fields are compared; three or more matching fields make a hit:

- First name and last name, each on its own. Both sides are normalized like division names (`DivisionNameTokenizer.Normalize`: lowercase, umlauts and accents dropped, "ae"/"oe"/"ue" folded, punctuation ignored) and split into words. A name matches when one side's words are all found on the other side, each word within an edit distance of 1 (2 for words of 8 letters or more). This catches typos, a missing umlaut, a dropped middle name ("Anna Maria" against "Anna") and swapped names ("Maria Anna" against "Anna Maria").
- Date of birth: equal.
- Street and house number: street normalized the same way with "strasse", "str" and "straße" folded into one word and within an edit distance of 2, and the house number equal ignoring spaces and case ("12a" against "12 A").
- Post code and city: the post code equal and the city within an edit distance of 1 after normalizing.
- Email: equal ignoring case and surrounding whitespace.

Fields that are empty on either side never match. Every entry with three or more matching fields is recorded as a hit in a new table `MembershipApplicationRegisterHits` (`ApplicationId`, `EntryId`, the matched fields, `DismissedAt`, `DismissedByUserId`). An application needs the federal approval while it has at least one hit that is not dismissed, or when the applicant ticked that an earlier application of theirs was rejected (`HasPriorDeclinedApplication`). The self-declaration is shown to the federal board as "nach eigener Angabe" and cannot be released with "Andere Person"; the Bundesvorstand decides.

When a hit comes in, the holders of the register permission get a new notification "Treffer im Ablehnungsregister" (on by default, through the usual channels), linking to the application, so they can mark "Andere Person" before the chapter decides. A self-declaration without a hit sends no such notification; the federal board learns of it through the federal motion.

## Flow when the decision happens in Quartermaster

- The chapter responsible gets its motion and processing dialog exactly as today. The chapter sees an information box on the application and in the motion text, "Benötigt zusätzlich die Zustimmung des Bundesvorstands (§ 3 Abs. 2 BS)", and nothing about the entry.
- Chapter rejects: the application is rejected as today (mail, register entry, outcome note).
- Chapter approves: instead of `Approved`, the application moves to a new status `PendingFederalApproval` (appended to `ApplicationStatus`). A second motion is created at the federal chapter, linked to the same application, titled "Mitgliedsantrag (Zustimmung nach § 3 Abs. 2 BS): <Name>". Its text holds the application data and the hearing: which chapter decided, when, and the vote (yes, no, abstain), or that it was decided in the processing dialog. It does not show the register entry, only a link to the application. The federal processors get the usual "Mitgliedsantrag eingegangen" notification.
- The Bundesvorstand decides through that motion or in the processing dialog. Approval sets `Approved`, and from there the SAGE entry and the approval mail run as today. Rejection sets `Rejected`, sends the rejection mail and adds a register entry.
- When the federal chapter itself is responsible (applicants abroad, or no lower chapter covers the address), there is only the one motion; it carries the same information box.
- A reduction in the application is decided separately as today and is not affected.

## Flow when the decision happens in Redmine

When the chapter responsible, or the federal chapter for its own applications, decides in Redmine, Quartermaster keeps no application and never sees the decision. A hit or a self-declaration is therefore written into the application's federal Redmine ticket: the Bundesvorstand must approve under § 3 Abs. 2 BS, with the matching entry (kind, date, name) and the matched fields, or "nach eigener Angabe". The rest is handled outside Quartermaster. The chapter's Redmine ticket gets no note. The notice is part of the federal ticket's description on every path, also when the decision happens in Quartermaster: an application that waits for its division has its federal ticket created before its path is known, and the federal ticket is read by the membership administration only. The hit notification goes out as on the Quartermaster path; its link leads to the register entry, since there is no application to open.

## "Andere Person"

On the application detail, people with the register permission see the hits: the matching entry and which fields matched. Each hit can be marked "Andere Person". Once no hit is left:

- before the chapter's decision: the application continues as a normal one, the information box disappears;
- after the chapter's approval (status `PendingFederalApproval`): the federal motion is closed without action (the audit log records why), and the application is approved on the chapter's decision.

Marking is audit logged and cannot be undone in the UI.

## Visibility and permission

A new global permission `applicant_register_manage` ("Ablehnungsregister verwalten"). Administrators grant it to the members of the Bundesvorstand; it is not part of any default role, so chapter officers never see it in their role or permission lists. It covers the register page, the hits on an application and "Andere Person". The chapter only ever sees that the federal approval is needed. Deciding at the federal level (the federal motion and the processing dialog of an application in `PendingFederalApproval`) needs the usual application permissions on the root chapter, never those of the chapter responsible.

## Confidentiality

Only the Bundesvorstand and the administrators need to know the register exists. It is not mentioned in the changelog (`Changelog/V9.0.md`), on the front page feature overview, or in the public knowledge base. The menu entry, the hit notification in the notification settings, and the hits on an application are shown only to holders of the permission; the notification catalog needs a per-trigger visibility check for that, since all triggers are listed for everyone today. Planning checks every other place that lists all triggers, templates or permissions for everyone and filters it the same way. The source code is public, so this keeps the register out of sight, not secret.

## Data and migration

Folded into `M009`: the two new tables, plus `ApplicationStatus.PendingFederalApproval`. Dev DB is altered by hand.

## Testing

- Matcher unit tests: each field rule, the three-field threshold, the examples above (umlaut, typo, dropped and swapped middle name), empty fields.
- Integration: a hit gets the information box; chapter approval creates the federal motion and holds approval, SAGE and mail; federal approval releases them; rejection at either level adds an entry; "Andere Person" before and after the chapter decision; the federal chapter as the only decider; no register data in chapter-level responses; a formal rejection adds no entry; the self-declaration requires the federal approval without a hit; a hit on a Redmine path adds the federal ticket note; the hit notification reaches only permission holders, and the trigger is listed only for them.

## Decisions

1. Redmine paths: a note in the federal ticket, handled outside Quartermaster (see above).
2. Formal rejections create no entry; only explicit decisions against the person and the cases of § 3 Abs. 2 BS are kept.
3. The self-declared earlier rejection requires the federal approval as well.
4. Post code and city must both match.
5. Register managers are notified of a hit right away.
6. The name is "Ablehnungsregister".
7. Not in the changelog or any public documentation.
