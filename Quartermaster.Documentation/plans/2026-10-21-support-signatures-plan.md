# UU-Assistent (support signatures) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Track support signature campaigns (counts only) from intake per municipality through shipments to certifying offices and their returns, print cover letters, and show members the progress with a downloadable form.

**Architecture:** A separate module `SupportSignatures` across Api (DTOs), Data (entities, repositories, migration `M008`), Server (pure calculators, services, FastEndpoints) and Blazor (admin pages, member page, start page card). The place data import gains the Destatis GV100AD file, which links municipalities to their Gemeindeverband so Samtgemeinden become office suggestions. Cover letters use the existing template system in envelope mode.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB on MariaDB, FluentMigrator, Fluid templates, QuestPDF, Blazor WASM, TUnit.

**Spec:** `Quartermaster.Documentation/plans/2026-10-21-support-signatures-design.md`

## Global Constraints

- Version V8.0: `Quartermaster.Server/Changelog/V8.0.md`, migration class `M008` with `[Migration(013)]`.
- No signer data: no names, addresses or form contents are stored; the note field carries the hint not to enter names.
- UI name "UU-Assistent"; UI terms Sammlung (campaign), Bestätigungsstelle (office), Sendung (shipment), Rücklauf (return). Code, routes and API stay English.
- Routes: `/Administration/SupportSignatures`, `/Administration/SupportSignatures/{Id:guid}`, `/Administration/SupportSignatures/Offices`, `/SupportSignatures/{Id:guid}`; API under `/api/support-signatures/...`.
- Permission `support_signatures_manage` (constant `PermissionIdentifier.ManageSupportSignatures`, display name "UU-Assistent: Sammlungen verwalten"), chapter-scoped, in `DefaultOfficerPermissions`.
- Campaign defaults: `MarginPercent` 20 (range 0 to 100), `OverdueDays` 21 (range 1 to 365), `Target` > 0; `Name` max 200, `MemberNotice` max 2,000, `Note` max 500.
- Form PDF at `data/support-signatures/{campaignId}.pdf`, PDF checked by content signature (`ReceiptFileType.Detect`), size limit option `support_signatures.form_max_megabytes` (default 10).
- Start page card shows campaigns that are not archived, visible to members, of the member's chapter or an ancestor, and whose deadline is at most 30 days past.
- Cover letter template identifier `templates.support_signatures.cover_letter`, render mode envelope.
- Project rules (CLAUDE.md and user): K&R braces and the if rules, explicit access modifiers, `var`, fluent LINQ, `using` directives, no `@code` in `.razor`, hand-written `ToDto()` mapping, comments only for the non-obvious why, no em or en dashes anywhere, only UI text in German via `de.json` and `en.json` with `I18nKey` constants, read-only inputs get both `readonly` and `disabled`.
- Builds and tests pinned to 4 cores: `taskset -c 0-3 nice -n 10 dotnet build Quartermaster.sln -v:q -nologo`, tests via `cd Quartermaster.Server.Tests && taskset -c 0-3 nice -n 10 dotnet run --no-build -- --treenode-filter "<filter>"`. Never run the full suite. No git commands; the user commits.

## Review Focus

- Two people preparing shipments for the same campaign at the same time: the second run must find no waiting forms instead of shipping them twice (test in Task 6).
- Correcting or deleting an intake after its forms were sent: refused, because waiting would go negative (test in Task 5).
- Several partial returns on one shipment line: their sum must never exceed the line, also when the second return alone would fit (test in Task 6).
- A campaign outside Lower Saxony or a municipality without GV100AD match (Bavarian Verwaltungsgemeinschaft, merged municipality): the office suggestion falls back to the municipality alone, no crash (test in Task 3).
- Chapter boundaries: a holder in Kreisverband A must not see or edit campaigns of Kreisverband B, a holder in the Landesverband sees both, and members only get cards of their own chapter line (tests in Tasks 4 and 8).

---

### Task 1: Data model, migration, permission

**Files:**
- Create: `Quartermaster.Api/SupportSignatures/SignatureRejectionReason.cs`
- Create: `Quartermaster.Data/SupportSignatures/` entities `SignatureCampaign.cs`, `SignatureIntake.cs`, `CertificationOffice.cs`, `CertificationOfficeMunicipality.cs`, `SignatureShipment.cs`, `SignatureShipmentLine.cs`, `SignatureReturn.cs`, `SignatureReturnLine.cs`
- Create: `Quartermaster.Data/AdministrativeDivisions/MunicipalityAssociation.cs`
- Create: `Quartermaster.Data/Migrations/M008.cs`
- Modify: `Quartermaster.Data/AdministrativeDivisions/AdministrativeDivision.cs` (add `MunicipalityAssociationKey`), `Quartermaster.Data/DbContext.cs` (tables), `Quartermaster.Api/PermissionIdentifier.cs`, `Quartermaster.Data/Permissions/PermissionRepository.cs`, `Quartermaster.Data/Options/OptionRepository.cs` (form size option), `Quartermaster.Blazor/Pages/Administration/Settings/SettingsAssistantCatalog.cs` (option placement)
- Create: `Quartermaster.Server/Changelog/V8.0.md`
- Test: `Quartermaster.Server.Tests/SupportSignatures/SupportSignatureSeedingTests.cs`

**Interfaces:**
- Produces: `enum SignatureRejectionReason { NoVotingRight = 0, NotResident = 1, IllegibleOrIncomplete = 2, SignedTwice = 3, Other = 4 }`; entity classes with the fields and max lengths of the spec's data model (table names `SignatureCampaigns`, `SignatureIntakes`, `CertificationOffices`, `CertificationOfficeMunicipalities`, `SignatureShipments`, `SignatureShipmentLines`, `SignatureReturns`, `SignatureReturnLines`, `MunicipalityAssociations`), `DbContext` properties of the same plural names; `PermissionIdentifier.ManageSupportSignatures`; option key `support_signatures.form_max_megabytes`.

- [ ] **Step 1: Write failing tests** `Officer_role_contains_the_support_signature_permission` (after `PermissionRepository.SupplementDefaults` and `RoleRepository.SupplementDefaults`, the officer role has `support_signatures_manage`, permission is not global) and `Form_size_option_defaults_to_10` (after `OptionRepository.SupplementDefaults`, `GetGlobalValue("support_signatures.form_max_megabytes")?.Value == "10"`).
- [ ] **Step 2: Build and run** filter `/*/*/SupportSignatureSeedingTests/*`; expected: compile error or FAIL.
- [ ] **Step 3: Implement** entities, `M008` (all tables with foreign keys to `Chapters`, `AdministrativeDivisions`, `Users` as in M007; unique index on `CertificationOfficeMunicipalities.AdministrativeDivisionId`; index on `SignatureIntakes (CampaignId, AdministrativeDivisionId)`; column `AdministrativeDivisions.MunicipalityAssociationKey` CHAR(9) nullable; `Down` drops everything), permission, officer list entry, option (OptionDataType.Number, description "Höchstgröße des Formulars einer UU-Sammlung in Megabyte."), catalog entry, changelog heading `### UU-Assistent` with a placeholder-free first paragraph describing the feature.
- [ ] **Step 4: Run** the filter plus `/*/*/SettingsAssistantCatalogTests/*`; expected PASS. Restart the dev server once and confirm `SELECT MAX(Version) FROM VersionInfo` is 13.
- [ ] **Step 5:** No commit (the user commits).

### Task 2: GV100AD import

**Files:**
- Create: `Quartermaster.Server/AdministrativeDivisions/Gv100AdParser.cs`, `Quartermaster.Server/AdministrativeDivisions/Gv100AdData.cs`
- Modify: `Quartermaster.Server/AdministrativeDivisions/AdminDivisionImportService.cs` (hash includes the GV100AD file; apply after the division import), `Quartermaster.Data/AdministrativeDivisions/AdministrativeDivisionRepository.cs`, `Quartermaster.Server/Quartermaster.Server.csproj` (content include), `Quartermaster.Blazor/Pages/Attributions/Attributions.razor` + i18n, `Quartermaster.Documentation/Configuration.md`
- Create: `Quartermaster.Server/GV100AD.txt` (bundled data file)
- Test: `Quartermaster.Server.Tests/AdministrativeDivisions/Gv100AdParserTests.cs`, `Quartermaster.Server.Tests/AdministrativeDivisions/Gv100AdImportTests.cs`, sample `Quartermaster.Server.Tests/TestData/gv100ad_sample.txt`

**Interfaces:**
- Produces: `record Gv100AdAssociation(string Key, string Name, int Kind)`, `record Gv100AdMunicipality(string Ags, string AssociationKey, string Name)`, `record Gv100AdData(IReadOnlyList<Gv100AdAssociation> Associations, IReadOnlyList<Gv100AdMunicipality> Municipalities)`; `static Gv100AdData Gv100AdParser.Parse(TextReader reader)`; `static bool Gv100AdParser.CertifiesForMembers(int kind)` (true for the kinds of real associations: Samtgemeinde and the other states' equivalents, read from the format PDF); `AdministrativeDivisionRepository.ApplyAssociations(Gv100AdData data, DateTime importedAt)` returning the number of unmatched municipalities; `AdministrativeDivisionRepository.GetAssociation(string key)` and `GetMunicipalitiesOfAssociation(string key)`.

- [ ] **Step 1: Obtain the file.** Ask the user to download the current GV100AD ZIP from Destatis (or approve the download); read the format PDF for the record layout and the Textkennzeichen of type 50 records; confirm the data licence. Cut a sample of 6 lines: Land Niedersachsen, Landkreis Celle, Samtgemeinde Lachendorf (type 50), its member municipalities Lachendorf `03351016` and Ahnsbeck (type 60), and the Einheitsgemeinde Celle `03351006` with its own type 50 entry.
- [ ] **Step 2: Write failing tests** `Parses_associations_and_municipalities` (sample gives Lachendorf with `AssociationKey` of the Samtgemeinde, `CertifiesForMembers(kind)` true for it and false for Celle's entry), `Applies_keys_by_padded_admin_code` (seeded divisions with `AdminCode` `3351016` and `3351006` get their keys; a seeded division `9999999` stays null and is counted as unmatched), `Reimport_with_unchanged_file_does_nothing`.
- [ ] **Step 3: Run** filters `/*/*/Gv100AdParserTests/*` and `/*/*/Gv100AdImportTests/*`; expected FAIL.
- [ ] **Step 4: Implement** the parser (fixed-width per the format PDF, UTF-8), `ApplyAssociations` (upsert `MunicipalityAssociations` by key, set keys on depth-7 divisions by `AdminCode.PadLeft(8, '0')`), the import hook (missing file: log a warning and skip, divisions still import) and the import log note "GV100AD: {n} Gemeinden ohne Zuordnung". Attribution with source and licence text on the sources page; Configuration.md paragraph on refreshing the file.
- [ ] **Step 5: Run** the filters plus `/*/*/AdminDivisionImport*/*`; expected PASS.
- [ ] **Step 6:** No commit.

### Task 3: Offices and suggestion

**Files:**
- Create: `Quartermaster.Data/SupportSignatures/CertificationOfficeRepository.cs`, `Quartermaster.Api/SupportSignatures/CertificationOfficeDTOs.cs`, `Quartermaster.Server/SupportSignatures/CertificationOfficeSuggester.cs`, endpoints in `Quartermaster.Server/SupportSignatures/Endpoints/Offices/` (`OfficeListEndpoint`, `OfficeCreateEndpoint`, `OfficeUpdateEndpoint`, `OfficeDeleteEndpoint`, `OfficeSuggestionEndpoint`), validator `CertificationOfficeSaveRequestValidator`
- Test: `Quartermaster.Server.Tests/SupportSignatures/CertificationOfficeSuggesterTests.cs`, `Quartermaster.Server.Tests/Integration/SupportSignatures/OfficeEndpointTests.cs`

**Interfaces:**
- Consumes: Task 2 `GetAssociation`, `GetMunicipalitiesOfAssociation`, `CertifiesForMembers`.
- Produces: `CertificationOfficeDTO { Id, Name, Street, PostCode, City, Email, Phone, Notes, List<OfficeMunicipalityDTO> Municipalities, bool HasShipments }`, `OfficeMunicipalityDTO { Id, Name }`, `CertificationOfficeSaveRequest { Name, Street, PostCode, City, Email, Phone, Notes, List<Guid> MunicipalityIds }`, `OfficeSuggestionDTO { Guid MunicipalityId, Guid? ExistingOfficeId, string SuggestedName, List<OfficeMunicipalityDTO> Municipalities }`; `CertificationOfficeRepository.GetOfficeIdsByMunicipality(IEnumerable<Guid> municipalityIds)` returning `Dictionary<Guid, Guid>`; routes `GET/POST /api/support-signatures/offices`, `PUT/DELETE /api/support-signatures/offices/{Id}`, `GET /api/support-signatures/offices/suggestion?municipalityId=`.

- [ ] **Step 1: Write failing tests:** suggester `Municipality_with_office_returns_it`, `Samtgemeinde_member_suggests_the_association_with_all_members` (name "Samtgemeinde Lachendorf", municipalities Lachendorf and Ahnsbeck), `Municipality_without_association_suggests_itself` (no key: name = municipality name, municipalities = itself; covers the Review Focus fallback), `Einheitsgemeinde_suggests_itself`. Integration: `Offices_need_the_permission_in_any_chapter` (401 anonymous, 403 without, 200 with it in one chapter), `Municipality_can_belong_to_one_office_only` (second office with the same municipality gets 409 `error.support_signatures.office.municipality_taken`), `Office_with_shipments_cannot_be_deleted` (409), `Save_validates_lengths` (name empty: 400).
- [ ] **Step 2: Run** filters; expected FAIL.
- [ ] **Step 3: Implement** repository, suggester, endpoints (permission via `PermissionContext.HasInAnyChapter(ManageSupportSignatures)`), audit log entries `CertificationOffice` Created/Updated/Deleted, i18n error keys.
- [ ] **Step 4: Run** filters; expected PASS.
- [ ] **Step 5:** No commit.

### Task 4: Campaigns, access, form file

**Files:**
- Create: `Quartermaster.Data/SupportSignatures/SignatureCampaignRepository.cs`, `Quartermaster.Api/SupportSignatures/SignatureCampaignDTOs.cs`, `Quartermaster.Server/SupportSignatures/SignatureCampaignAccess.cs`, `Quartermaster.Server/SupportSignatures/SupportSignatureFormStorage.cs`, endpoints in `Quartermaster.Server/SupportSignatures/Endpoints/Campaigns/` (`CampaignListEndpoint`, `CampaignCreateEndpoint`, `CampaignGetEndpoint`, `CampaignUpdateEndpoint`, `CampaignArchiveEndpoint`, `CampaignFormUploadEndpoint`, `CampaignFormDeleteEndpoint`), validator `SignatureCampaignSaveRequestValidator`
- Modify: `Quartermaster.Server/Program.cs` (DI)
- Test: `Quartermaster.Server.Tests/Integration/SupportSignatures/CampaignEndpointTests.cs`, `Quartermaster.Server.Tests/SupportSignatures/SignatureCampaignValidatorTests.cs`

**Interfaces:**
- Produces: `SignatureCampaignSaveRequest { Guid ChapterId, string Name, int Target, int MarginPercent, DateTimeOffset Deadline, int OverdueDays, bool VisibleToMembers, string? MemberNotice }`; `SignatureCampaignDTO { Id, ChapterId, ChapterName, Name, Target, MarginPercent, Deadline, OverdueDays, VisibleToMembers, MemberNotice, HasForm, FormFileName, ArchivedAt, SignatureTotalsDTO Totals, int OverdueShipmentCount }` (Totals and overdue count filled from Task 5 and 6; zeros until then); `SignatureCampaignAccess.CanManage(SignatureCampaign campaign)` (`PermissionContext.Has(campaign.ChapterId, ManageSupportSignatures)`), `SignatureCampaignAccess.ManageableChapterIds()` (`GetPermittedChapterIds`); `SupportSignatureFormStorage.SaveAsync(Guid campaignId, Stream pdf, CancellationToken ct)`, `OpenRead(Guid campaignId)`, `Delete(Guid campaignId)`; routes `GET/POST /api/support-signatures/campaigns`, `GET/PUT /api/support-signatures/campaigns/{Id}`, `POST .../{Id}/archive`, `PUT/DELETE .../{Id}/form`.

- [ ] **Step 1: Write failing tests:** validator: target 0, margin 101, overdue days 0, name 201 chars, notice 2,001 chars each give their error key. Integration: `Kreisverband_holder_sees_only_own_campaigns` and `Landesverband_holder_sees_campaigns_of_sub_chapters` (Review Focus), `Create_needs_permission_on_the_chosen_chapter` (403), `Archive_hides_from_default_list_and_shows_with_filter`, `Form_upload_accepts_pdf_and_rejects_png_and_oversize` (PNG bytes: 400 `error.support_signatures.form.not_pdf`; 11 MB: 400 `error.support_signatures.form.too_large`), `Form_upload_replaces_the_previous_file`.
- [ ] **Step 2: Run** filters; expected FAIL.
- [ ] **Step 3: Implement** repository, access, storage (directory `AppContext.BaseDirectory/data/support-signatures`, tests pass a temp root through the constructor like `ReceiptStorage`), endpoints with audit log entries (`SignatureCampaign` Created, field changes, Archived, FormUploaded, FormDeleted), validator messages in i18n.
- [ ] **Step 4: Run** filters; expected PASS.
- [ ] **Step 5:** No commit.

### Task 5: Intakes, tally, overview

**Files:**
- Create: `Quartermaster.Server/SupportSignatures/SignatureTally.cs`, `Quartermaster.Data/SupportSignatures/SignatureIntakeRepository.cs`, `Quartermaster.Api/SupportSignatures/SignatureIntakeDTOs.cs`, `Quartermaster.Api/SupportSignatures/SignatureOverviewDTOs.cs`, `Quartermaster.Server/SupportSignatures/SignatureOverviewBuilder.cs`, endpoints in `.../Endpoints/Intakes/` (`IntakeListEndpoint`, `IntakeCreateEndpoint`, `IntakeUpdateEndpoint`, `IntakeDeleteEndpoint`) and `.../Endpoints/Campaigns/CampaignOverviewEndpoint.cs`
- Test: `Quartermaster.Server.Tests/SupportSignatures/SignatureTallyTests.cs`, `Quartermaster.Server.Tests/Integration/SupportSignatures/IntakeEndpointTests.cs`

**Interfaces:**
- Consumes: Task 4 access; Task 1 entities (shipment and return tables exist, filled from Task 6).
- Produces: `record MunicipalityTally(int Received, int AlreadyCertified, int Sent, int Certified, int Rejected)` with `Waiting => Received - AlreadyCertified - Sent`, `Open => Sent - Certified - Rejected`, `CertifiedTotal => AlreadyCertified + Certified`; `static Dictionary<Guid, MunicipalityTally> SignatureTally.PerMunicipality(IEnumerable<SignatureIntake> intakes, IEnumerable<SignatureShipmentLine> sentLines, IEnumerable<SignatureReturnLine> returnLines)` (only lines of shipments with `SentOn` set count as sent); `SignatureTally.Load(DbContext db, Guid campaignId)` returning the same dictionary; `SignatureTotalsDTO { Received, AlreadyCertified, Waiting, AtOffices, Certified, Rejected, Target, Goal }` with `Goal = (int)Math.Ceiling(Target * (1 + MarginPercent / 100.0))`; `RejectionCountDTO { SignatureRejectionReason Reason, int Count }`, `SignatureOverviewDTO { SignatureTotalsDTO Totals, List<RejectionCountDTO> Rejections, List<SignatureRegionRowDTO> Regions, int DaysLeft }`, `SignatureRegionRowDTO { DivisionId, Name, Received, Certified, Rejected, List<SignatureRegionRowDTO> Children }` (Landkreis rows with municipality children); `SignatureIntakeSaveRequest { Guid AdministrativeDivisionId, int Count, bool AlreadyCertified, DateOnly ReceivedOn, string? Note }`; routes `GET/POST /api/support-signatures/campaigns/{Id}/intakes`, `PUT/DELETE /api/support-signatures/intakes/{Id}`, `GET /api/support-signatures/campaigns/{Id}/overview`.

- [ ] **Step 1: Write failing tests:** tally `Waiting_and_open_follow_the_spec` (Received 13 of which AlreadyCertified 3, Sent 8, Certified 5, Rejected 1 gives Waiting 2, Open 2, CertifiedTotal 8), `Prepared_shipments_do_not_count_as_sent`, `Goal_rounds_up` (target 2,000, margin 20: 2,400; target 100, margin 15: 115). Integration: `Intake_needs_a_municipality` (a depth-6 division: 400), `Update_or_delete_refused_when_forms_were_sent` (Review Focus: after a confirmed shipment of all waiting forms, lowering the count gives 409 `error.support_signatures.intake.already_sent`), `Overview_groups_by_landkreis`, `Intakes_of_a_foreign_chapter_campaign_are_forbidden`.
- [ ] **Step 2: Run** filters; expected FAIL.
- [ ] **Step 3: Implement** repository, tally, overview builder, endpoints; the update and delete check recomputes the municipality's waiting count with the changed intake inside a transaction that locks the campaign row (`SELECT ... FOR UPDATE` like `AssemblyMotionRepository.LockType`); fill `SignatureCampaignDTO.Totals` in the list and get endpoints.
- [ ] **Step 4: Run** filters; expected PASS.
- [ ] **Step 5:** No commit.

### Task 6: Shipments and returns

**Files:**
- Create: `Quartermaster.Server/SupportSignatures/ShipmentPlanner.cs`, `Quartermaster.Server/SupportSignatures/SignatureShipmentService.cs`, `Quartermaster.Data/SupportSignatures/SignatureShipmentRepository.cs`, `Quartermaster.Api/SupportSignatures/SignatureShipmentDTOs.cs`, endpoints in `.../Endpoints/Shipments/` (`ShipmentPlanEndpoint`, `ShipmentPrepareEndpoint`, `ShipmentCreateSingleEndpoint`, `ShipmentConfirmEndpoint`, `ShipmentDiscardEndpoint`, `ShipmentListEndpoint`, `ReturnCreateEndpoint`, `ReturnDeleteEndpoint`)
- Test: `Quartermaster.Server.Tests/SupportSignatures/ShipmentPlannerTests.cs`, `Quartermaster.Server.Tests/SupportSignatures/SignatureShipmentServiceTests.cs`, `Quartermaster.Server.Tests/Integration/SupportSignatures/ShipmentEndpointTests.cs`

**Interfaces:**
- Consumes: Task 3 `GetOfficeIdsByMunicipality`; Task 5 `SignatureTally.Load`, `MunicipalityTally`.
- Produces: `record ShipmentPlan(IReadOnlyList<PlannedShipment> Offices, IReadOnlyList<PlannedLine> Unassigned)`, `record PlannedShipment(Guid OfficeId, IReadOnlyList<PlannedLine> Lines)`, `record PlannedLine(Guid MunicipalityId, int Count)`; `static ShipmentPlan ShipmentPlanner.Plan(IReadOnlyDictionary<Guid, MunicipalityTally> tallies, IReadOnlyDictionary<Guid, Guid> officeByMunicipality)` (only `Waiting > 0`); `SignatureShipmentService.Prepare(Guid campaignId, IReadOnlyCollection<Guid> officeIds, Guid userId)` returning the created shipment ids; `CreateSingle(Guid campaignId, Guid officeId, IReadOnlyList<PlannedLine> lines, Guid userId)`; `Confirm(Guid shipmentId, DateOnly sentOn)`; `Discard(Guid shipmentId)` (prepared only); `RecordReturn(Guid shipmentId, DateOnly returnedOn, IReadOnlyList<ReturnLineInput> lines, Guid userId)`; `DeleteReturn(Guid returnId)`; all returning `SupportSignatureOutcome` (`Ok`, `Fail(int status, string errorKey)`, like `AssemblyMotionOutcome`). DTOs `ShipmentPlanDTO`, `SignatureShipmentDTO { Id, OfficeId, OfficeName, SentOn, List<ShipmentLineDTO> Lines, int Open, bool IsOverdue }`, `ShipmentLineDTO { MunicipalityId, MunicipalityName, Sent, Certified, Rejected, Open }`, `ReturnSaveRequest { DateOnly ReturnedOn, List<ReturnLineInput> Lines }`, `ReturnLineInput { Guid MunicipalityId, int Certified, int Rejected, SignatureRejectionReason? RejectionReason }`. Routes `GET .../campaigns/{Id}/shipment-plan`, `POST .../campaigns/{Id}/shipments/prepare`, `POST .../campaigns/{Id}/shipments`, `GET .../campaigns/{Id}/shipments`, `POST /api/support-signatures/shipments/{Id}/confirm`, `DELETE /api/support-signatures/shipments/{Id}`, `POST /api/support-signatures/shipments/{Id}/returns`, `DELETE /api/support-signatures/returns/{Id}`.

- [ ] **Step 1: Write failing tests:** planner `Groups_waiting_forms_by_office`, `Municipalities_without_office_are_unassigned`, `Nothing_waiting_gives_an_empty_plan`. Service: `Second_prepare_finds_nothing` (Review Focus: prepare twice for the same offices, second creates no shipment, and the same with two DbContexts in parallel like `AssemblyMotionServiceTests` does with `NewServiceOn`), `Single_shipment_cannot_exceed_waiting` (409 `error.support_signatures.shipment.exceeds_waiting`), `Discard_only_prepared`, `Partial_returns_cannot_exceed_the_line` (Review Focus: line 10, return 6 ok, return 5 refused with `error.support_signatures.return.exceeds_open`, return 4 ok), `Rejected_needs_a_reason_only_when_greater_than_zero`, `Overdue_after_overdue_days_with_open_forms`. Integration: `Shipments_of_a_foreign_chapter_campaign_are_forbidden`, `Prepare_returns_the_new_ids_and_list_shows_them_unconfirmed`.
- [ ] **Step 2: Run** filters; expected FAIL.
- [ ] **Step 3: Implement** planner, repository, service (each mutating method in one transaction that first locks the campaign row), endpoints, audit log entries (`SignatureShipment` Prepared/Confirmed/Discarded, `SignatureReturn` Created/Deleted); fill `SignatureCampaignDTO.OverdueShipmentCount`.
- [ ] **Step 4: Run** filters plus Task 5's; expected PASS.
- [ ] **Step 5:** No commit.

### Task 7: Cover letter

**Files:**
- Create: `Quartermaster.Api/SupportSignatures/SupportSignatureLetterModel.cs`, `Quartermaster.Server/SupportSignatures/CoverLetterRenderer.cs`, `Quartermaster.Server/SupportSignatures/Endpoints/Shipments/ShipmentLettersEndpoint.cs`
- Modify: `Quartermaster.Data/Templates/SystemTemplateSeeds.cs` and `SystemTemplateSeed` (add `TemplateRenderMode RenderMode = TemplateRenderMode.Simple`), `Quartermaster.Data/Templates/TemplateRepository.cs` (`SupplementDefaults` sets it), `Quartermaster.Api/Templates/TemplateModelLookup.cs`, `Quartermaster.Rendering/TemplateMockDataProvider.cs`, `Quartermaster.Server/Templates/TemplateEnvelopePdfRenderer.cs`
- Test: `Quartermaster.Server.Tests/SupportSignatures/CoverLetterRendererTests.cs`, `Quartermaster.Server.Tests/Templates/TemplateEnvelopePdfRendererTests.cs` (extend if present)

**Interfaces:**
- Consumes: Task 6 shipments, Task 3 offices.
- Produces: `SupportSignatureLetterModel { Campaign { Name, Deadline }, Office { Name, Street, PostCode, City }, List<Line { Municipality, Count }> Lines, int Total, string SentOn }` exposed to Fluid as `letter` plus `chapter` (`ChapterDTO`); `TemplateEnvelopePdfRenderer.RenderMany(IReadOnlyList<(EnvelopeData Envelope, string Body)> letters)` returning one PDF with one letter per page group (refactor `Render` to call it with one letter); `CoverLetterRenderer.RenderAsync(SignatureCampaign campaign, IReadOnlyList<Guid> shipmentIds, CancellationToken ct)` returning `byte[]`, resolving the template with `TemplateRepository.Resolve("templates.support_signatures.cover_letter", campaign.ChapterId, chapterRepo)`; route `GET /api/support-signatures/campaigns/{Id}/shipments/letters?ids=...` (PDF, file name `Anschreiben-{campaign}.pdf`).

- [ ] **Step 1: Write failing tests:** `Renders_one_letter_per_shipment` (two shipments give a PDF whose text contains both office names and per line "Lachendorf: 12"), `Uses_the_chapter_override`, `Mock_model_renders_in_the_template_editor` (`TemplatePdfRenderer.RenderAsync` on the seeded template returns a PDF without error), `RenderMany_with_one_letter_matches_Render`.
- [ ] **Step 2: Run** filters; expected FAIL.
- [ ] **Step 3: Implement** seed (display name "UU-Assistent: Anschreiben an die Bestätigungsstelle", envelope tags filled from `chapter` as sender and `letter.Office` as recipient, body: request to certify the voting rights of the signers of the enclosed forms for "{{ letter.Campaign.Name }}" and return them, the line list, total, contact), lookup entry for identifiers starting with `templates.support_signatures.`, mock data, renderer, endpoint (campaign access check; ids must belong to the campaign).
- [ ] **Step 4: Run** filters plus `/*/*/TemplatePdf*/*`; expected PASS.
- [ ] **Step 5:** No commit.

### Task 8: Member endpoints and dashboard data

**Files:**
- Create: `Quartermaster.Api/SupportSignatures/MemberSignatureCampaignDTO.cs`, endpoints `.../Endpoints/Members/MemberCampaignGetEndpoint.cs`, `.../Endpoints/Members/MemberCampaignFormEndpoint.cs`, `Quartermaster.Server/SupportSignatures/MemberSignatureCampaigns.cs`
- Modify: `Quartermaster.Api/Dashboard/DashboardDTO.cs` (`List<MemberSignatureCampaignDTO> SupportSignatureCampaigns`), `Quartermaster.Server/Dashboard/DashboardEndpoint.cs`
- Test: `Quartermaster.Server.Tests/Integration/SupportSignatures/MemberCampaignEndpointTests.cs`

**Interfaces:**
- Consumes: Task 4 storage, Task 5 tally.
- Produces: `MemberSignatureCampaignDTO { Id, Name, ChapterName, Target, Goal, CertifiedTotal, Deadline, HasForm, MemberNotice }`; `MemberSignatureCampaigns.ForMember(Member member, DateTime nowUtc)` (rules from Global Constraints); routes `GET /api/support-signatures/member-campaigns/{Id}`, `GET /api/support-signatures/member-campaigns/{Id}/form`.

- [ ] **Step 1: Write failing tests:** `Member_gets_cards_of_own_chapter_line_only` (Review Focus: campaigns in own KV and the LV yes, sibling KV no), `Hidden_archived_and_long_past_campaigns_are_left_out` (31 days past: absent, 30 days: present), `Anonymous_gets_401`, `Form_download_sends_the_original_file_name`, `Unrelated_campaign_gives_404`.
- [ ] **Step 2: Run** filters; expected FAIL.
- [ ] **Step 3: Implement**; the member's chapter comes from the linked member, ancestors via `ChapterRepository`.
- [ ] **Step 4: Run** filters plus `/*/Quartermaster.Server.Tests.Integration.Dashboard/*/*`; expected PASS.
- [ ] **Step 5:** No commit.

### Task 9: Admin pages

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/SupportSignatures/SupportSignatureCampaigns.razor(.cs)`, `SupportSignatureCampaign.razor(.cs)` with tab components `CampaignOverviewTab`, `CampaignIntakeTab`, `CampaignShipmentsTab`, `CampaignSettingsTab` (each `.razor` + `.razor.cs` in `Quartermaster.Blazor/Components/SupportSignatures/`), `CertificationOffices.razor(.cs)`, `OfficeAssignDialog.razor(.cs)`
- Modify: `Quartermaster.Blazor/Layout/MainNavBar.razor` (entry under "Vorstandsarbeit" with `RequirePermission AnyChapterPermission="@PermissionIdentifier.ManageSupportSignatures"`, and the permission in the dropdown's `AnyOfPermissions`), `de.json`, `en.json`, `I18nKey.cs`, `Quartermaster.Documentation/Translations.md`
- Test: build only plus a manual check list in the report; no component tests exist in the project.

**Interfaces:**
- Consumes: all endpoints of Tasks 3 to 7.

- [ ] **Step 1: Implement** the pages as the spec's "Pages for permission holders" section describes: intake form with `MunicipalityPicker` (`OnSelect`), count, already certified, date, note with the hint "Bitte keine Namen eintragen.", focus back to the picker after saving; shipments tab with the plan, office selection, "Anschreiben drucken" (opens the letters PDF for the prepared ids), "Als versendet bestätigen", "Verwerfen", unassigned municipalities with "Bestätigungsstelle zuordnen" (dialog using the suggestion endpoint), open shipments by age with overdue badge, return dialog per line; settings tab with all fields, form upload and archive; offices page with list, edit and municipality chips.
- [ ] **Step 2: Build** the solution; expected: no errors. Restart the dev server.
- [ ] **Step 3: Check by hand** with seeded dummy data (one LV campaign, intakes in two Samtgemeinde member municipalities and one Einheitsgemeinde): prepare, print, confirm, record a partial return; note any rough edges in the report for the user's later pass.
- [ ] **Step 4:** No commit.

### Task 10: Member page and start page card

**Files:**
- Create: `Quartermaster.Blazor/Pages/SupportSignatures/SupportSignatureCampaignPublic.razor(.cs)` (route `/SupportSignatures/{Id:guid}`), `Quartermaster.Blazor/Components/SupportSignatures/SupportSignatureProgressCard.razor(.cs)`
- Modify: `Quartermaster.Blazor/Pages/Home.razor` (cards from `Dashboard.SupportSignatureCampaigns`, using `DashboardCard`), i18n files, `Quartermaster.Server/Changelog/V8.0.md`, `Quartermaster.Documentation/Configuration.md` (UU-Assistent section: offices, GV100AD refresh, option, template)

**Interfaces:**
- Consumes: Task 8 DTO and routes.

- [ ] **Step 1: Implement** the card ("{certified} von {target} bestätigt", progress bar with the goal mark, deadline via `DeadlineText`, links to the page and the form) and the member page (numbers, `MemberNotice` rendered as Markdown with the Strict profile, form download).
- [ ] **Step 2: Build**, restart, check the start page as a member of the campaign's chapter and as one of a sibling chapter.
- [ ] **Step 3: Final checks:** run filters `/*/*/SupportSignature*/*`, `/*/Quartermaster.Server.Tests.Integration.SupportSignatures/*/*`, `/*/*/Gv100Ad*/*`, `/*/*/CertificationOffice*/*`, `/*/*/Signature*/*`, `/*/*/ShipmentPlanner*/*`, `/*/*/CoverLetter*/*`; sweep the diff against CLAUDE.md; grep the new files for em and en dashes.
- [ ] **Step 4:** No commit.
