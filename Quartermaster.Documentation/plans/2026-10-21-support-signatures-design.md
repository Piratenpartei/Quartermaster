# Support signatures (Unterstützungsunterschriften) (V8.0)

## Goal

Parties without seats need support signatures to stand in an election. For the Lower Saxony Landtag election the state list needs 2,000 signatures, every direct candidate 100 from their constituency, and every form has to be certified by the signer's municipality before submission. Quartermaster tracks the campaign without storing a single form or any signer data: how many forms came in per municipality, which went to which certifying office, what came back certified or rejected, and how far that is from the target. It prints the cover letters for the offices, offers the blank form for download, and shows members the progress.

## Decisions

- Only counts are stored. No forms, no names, no addresses of signers.
- A campaign is one nomination with one target: the state list is one campaign, every direct candidate gets their own.
- Every campaign belongs to a chapter. The permission `support_signatures_manage` (chapter-scoped, part of the board role, assignable to any role so Beauftragte can get it) works on campaigns of that chapter and its sub-chapters.
- Forms are counted per municipality, picked with the municipality picker (postcode or name). The region hierarchy (Landkreis, municipality) gives the statistics.
- Forms are sent to certifying offices. An office covers one or more municipalities; in Lower Saxony a Samtgemeinde certifies for its member municipalities. Offices form one address book shared by all campaigns.
- The grouping of municipalities into Samtgemeinden comes from the Destatis GV100AD file, matched through the 8-digit municipality key (AGS). Addresses are entered by hand, once per office.
- Shipments are tracked individually: sent date, forms per municipality, returns with certified and rejected per municipality, partial returns allowed, rejections with an optional reason.
- Forms that arrive already certified skip the shipment.
- Shipments are prepared in a batch run (one shipment and one cover letter per office with waiting forms) or created singly.
- The whole list is submitted at once, so submission is not tracked.
- Logged-in members see a progress card per visible campaign and can download the form; switchable per campaign. Nothing is public without login.
- Separate module (no reuse of events), released as V8.0 with migration `M008`.
- UI name: "UU-Assistent" (the party calls the signatures "UUs"). A campaign is a "Sammlung" in the UI, an office a "Bestätigungsstelle", a shipment a "Sendung", a return a "Rücklauf". Code and routes stay English (`SupportSignatures`).

## Data model

All tables are new except the two additions to the place data.

- `SignatureCampaigns`: `Id`, `ChapterId`, `Name` (max 200), `Target` (int > 0), `MarginPercent` (int 0 to 100, default 20), `Deadline` (datetime, Berlin instant as elsewhere), `OverdueDays` (int, default 21), `VisibleToMembers` (bool), `MemberNotice` (Markdown, max 2,000, optional), `HasForm` (bool), `FormFileName` (original name, max 255, optional), `ArchivedAt` (nullable), `CreatedAt`.
- `SignatureIntakes`: `Id`, `CampaignId`, `AdministrativeDivisionId` (a municipality, depth 7), `Count` (int > 0), `AlreadyCertified` (bool), `ReceivedOn` (date), `Note` (max 500, optional), `CreatedAt`, `CreatedByUserId`.
- `CertificationOffices`: `Id`, `Name` (max 200), `Street`, `PostCode`, `City` (each max 200), `Email` and `Phone` (optional), `Notes` (optional, max 1,000).
- `CertificationOfficeMunicipalities`: `OfficeId`, `AdministrativeDivisionId`, unique on the municipality (a municipality has at most one office).
- `SignatureShipments`: `Id`, `CampaignId`, `OfficeId`, `SentOn` (date, null while prepared but not yet confirmed), `CreatedAt`, `CreatedByUserId`.
- `SignatureShipmentLines`: `ShipmentId`, `AdministrativeDivisionId`, `Count` (> 0).
- `SignatureReturns`: `Id`, `ShipmentId`, `ReturnedOn` (date), `CreatedAt`, `CreatedByUserId`.
- `SignatureReturnLines`: `ReturnId`, `AdministrativeDivisionId`, `Certified` (>= 0), `Rejected` (>= 0), `RejectionReason` (enum, nullable: `NoVotingRight`, `NotResident`, `IllegibleOrIncomplete`, `SignedTwice`, `Other`). A line with rejections of several reasons is entered as several lines.
- Place data: `AdministrativeDivisions.MunicipalityAssociationKey` (9 chars, nullable) on municipalities, and a table `MunicipalityAssociations`: `Key` (9 chars: Land, Regierungsbezirk, Kreis, Gemeindeverband), `Name`, `Kind` (the GV100AD Textkennzeichen), `ImportedAt`.

Derived numbers, per campaign and per municipality:

- received = sum of intakes; already certified = sum of intakes with `AlreadyCertified`.
- sent = sum of lines of shipments with `SentOn` set.
- waiting = received - already certified - sent (prepared but unconfirmed shipments count as waiting).
- returned = certified + rejected over all return lines; open at offices = sent - returned.
- certified total = already certified + certified from returns; this is the number measured against `Target` and `Target * (1 + MarginPercent / 100)`.
- A shipment is overdue when `SentOn + OverdueDays` lies in the past and it still has open forms.

## GV100AD import

- The quarterly GV100AD file from Destatis (`GV100AD_DDMMYY.txt`, fixed width, UTF-8) is bundled with the server next to `DE_Base.txt` and `DE_PostCodes.txt` and imported by the existing administrative division import whenever its hash changes.
- Record type 50 (Gemeindeverband) fills `MunicipalityAssociations`; record type 60 (Gemeinde) sets `MunicipalityAssociationKey` on the municipality whose `AdminCode`, padded to 8 digits, equals Land + Regierungsbezirk + Kreis + Gemeinde of the record. Column positions are taken from the format description in the Destatis ZIP; a parser test pins them against a cut-down sample file.
- Municipalities without a match are counted in the import log and shown on the import status page; they get their office by hand.
- Only associations that really certify for several municipalities are used as office suggestions (in Lower Saxony the Samtgemeinden); the Textkennzeichen values for that are taken from the format description and kept in one place.
- The source and licence (expected: Datenlizenz Deutschland - Namensnennung 2.0, to be confirmed from the Destatis site) go onto the sources page next to GeoNames.

## Offices

- When forms of a municipality without an office are to be sent, the UI offers its association's office if one exists, or creates one from the association (name prefilled, address to fill in) and assigns all member municipalities of that association at once. For a municipality without association the office is the municipality's own administration.
- Offices are edited in the address book; municipalities can be added or removed by hand. A municipality belongs to one office at most.
- An office with shipments cannot be deleted.

## Shipments and returns

- "Prepare shipments" lists every office with waiting forms of the campaign, with the counts per municipality; municipalities without office are listed separately with an "assign office" action. The user selects offices, Quartermaster creates one shipment per selected office (lines = all waiting forms of its municipalities) with `SentOn` null, and returns one PDF with all cover letters. "Confirm sent" sets `SentOn`; "discard" deletes prepared shipments. Preparing and confirming run in a transaction that locks the campaign row, so waiting forms are never put into two shipments.
- "New single shipment": pick an office, adjust the counts per municipality (at most the waiting forms), print its letter, confirm.
- "Record return" on a sent shipment: per line certified and rejected (with reason); the sum per municipality over all returns can never exceed the line's count. Returns can be corrected or deleted.
- Intakes can be corrected or deleted only as long as the forms are still waiting, i.e. the municipality's waiting count stays >= 0.

## Cover letter

- New PDF template `templates.support_signatures.cover_letter`, envelope mode (DIN 5008), overridable per chapter like the other templates. Default text: request to certify the voting rights of the enclosed signers for the named nomination and to return the forms to the sender address; list of municipalities with form counts; sender chapter and contact.
- Template model `SupportSignatureLetterModel`: campaign (name, deadline), office (name, address), lines (municipality, count), total, chapter (`ChapterDTO`), sent date. Registered in `TemplateModelLookup` and the mock data for the template editor.

## Members

- Start page card per active (not archived, deadline not passed by more than 30 days), visible campaign of the member's chapter and its ancestors: "{certified} von {target} bestätigt", progress bar (with the margin as a second mark), deadline, links to the campaign page and the form download.
- Member campaign page `/SupportSignatures/{id}`: the same numbers, the `MemberNotice` and the form download. No regions, no offices.
- Member endpoints need a login only and answer 404 for campaigns that are archived, hidden or of an unrelated chapter.

## Pages for permission holders

Menu "Vorstandsarbeit" > "UU-Assistent", visible with the permission in any chapter.

- `/Administration/SupportSignatures`: campaigns of the user's chapters with progress, deadline and overdue warning; new campaign; filter for archived ones.
- `/Administration/SupportSignatures/{id}`, tabs:
  - Overview: funnel (received, waiting, at offices, certified, rejected), rejection reasons, days left, table by Landkreis expanding to municipalities.
  - Enter forms: municipality picker, count, "already certified", date (today), note with the hint not to enter names; focus returns to the picker after saving; latest entries with edit and delete.
  - Shipments: prepare shipments, new single shipment, open shipments by age with overdue highlighted, record return.
  - Settings: all campaign fields, form PDF upload and removal, archive.
- `/Administration/SupportSignatures/Offices`: the address book.

## Server

- Endpoints under `/api/support-signatures/...` (FastEndpoints). Campaign endpoints check the permission against the campaign's chapter (`PermissionContext`, with inheritance to ancestors). Office endpoints need the permission in any chapter. Member endpoints need authentication only.
- Form PDF: stored at `data/support-signatures/{campaignId}.pdf`, PDF only (checked by content, like receipts), size limit from the new option `support_signatures.form_max_megabytes` (default 10). Download sends it with the stored original file name.
- Audit log entries for campaign settings, offices, shipments (prepared, confirmed, discarded) and returns.
- No personal data is stored; notes are free text and carry the hint.

## Tests

- Unit: derived numbers (waiting, open, certified total, overdue), shipment preparation from intakes and offices, return limits, GV100AD parser against a sample file, office suggestion from associations.
- Integration: permission per chapter and inheritance, office permission, member endpoints (visibility, archived, unrelated chapter), double preparation of the same waiting forms (second run gets nothing), intake deletion refused once sent, return exceeding the line refused, form upload type and size checks.
- Template: the cover letter renders against the mock model.

## Docs

- `Configuration.md`: GV100AD file and its refresh, the new option, the cover letter template.
- `Translations.md`: UU-Assistent (support signature assistant), UU / Unterstützungsunterschrift (support signature), Sammlung (campaign), Bestätigungsstelle (certification office), Sendung (shipment), Rücklauf (return), bestätigt / abgelehnt and the rejection reasons.
- `Changelog/V8.0.md`.
- Sources page: GV100AD attribution.
