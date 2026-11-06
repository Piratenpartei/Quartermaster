# UI Modernisation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every page of Quartermaster use one set of building blocks (page header, buttons, AsyncButton, cards, tables, badges) and work on a phone without sideways scrolling.

**Architecture:** A small foundation in `Quartermaster.Blazor/Components` (extended `PageHeader`, new `AsyncButton`, `StatusBadge`, `ResponsiveList`, a merged account menu) on Bootstrap 5.3.8, then all pages converted group by group against the conventions page. A rule test scans Razor files for the forbidden patterns; the headless screenshot audit (outside the repository) checks for sideways scrolling.

**Tech Stack:** .NET 10, Blazor WASM, Bootstrap 5.3.8, Bootstrap Icons 1.13.2, TUnit.

**Spec:** `Quartermaster.Documentation/plans/2026-11-06-ui-modernisation-design.md`

## Global Constraints

- Bootstrap 5.3.8 and Bootstrap Icons 1.13.2, vendored like today.
- Bootstrap classes wherever possible; customisation through CSS variables in `piratenpartei-theme.css`; own CSS only for the AsyncButton width.
- Main action `btn-primary` (one per page header, one per form); others `btn-outline-secondary`; destructive `btn-outline-danger`, filled `btn-danger` only in confirmation dialogs; no `btn-outline-primary`; no `btn-hover-scale1`.
- Normal button size in headers, forms and cards; `btn-sm` only in table rows and lists; icon plus text, icon-only only in table rows with `title` and `aria-label`.
- Button and badge rows: `d-flex flex-wrap gap-2`.
- Sections: `card mb-4`, title `h5` in `card-body`, no nested cards, no inline styles.
- Tables: `table table-hover align-middle` (`table-sm` for dense admin lists), inside `table-responsive`; member-facing lists via `ResponsiveList`.
- Key-value blocks: `dl class="row"` with `dt class="col-sm-4"` and `dd class="col-sm-8"`.
- Forms: label above, `form-text` below, `row g-3` with `col-md-*`, read-only fields `readonly` plus `disabled`, form buttons at the bottom of the card.
- No `@code` in `.razor`; CLAUDE.md style; German UI texts in the generic masculine; no em or en dashes.
- No git commands; build with `taskset -c 0-9 dotnet build Quartermaster.sln`; never run the full test suite, only the named groups.
- Changelog lines go to `Quartermaster.Server/Changelog/V9.2.md` (version decided later; rename if it becomes V10).

## Review Focus

- Long German texts (button labels, account menu, page titles) at 390 px: they wrap, never push the page sideways. Checked by the audit rerun in Task 11.
- Dark mode: soft badge colours and the AsyncButton done and failed states stay readable. Checked by the dark screenshots in Task 11.
- A page left during the two seconds after an AsyncButton finished: no exception, no state change on a disposed component. Pinned by `AsyncButtonState` tests for reset after disposal (Task 3).
- Pages where a success toast was replaced by an AsyncButton: a failure still shows the server's reason as a toast. Checked per page in Tasks 8 to 10 (step "keep error toasts").
- Member lists with long titles on a phone: the stacked `ResponsiveList` rows wrap the title. Checked by the audit rerun in Task 11.

---

### Task 1: Bootstrap 5.3.8 and Bootstrap Icons 1.13.2

**Files:**
- Create: `Quartermaster.Blazor/wwwroot/css/bootstrap-5.3.8-dist/` (from the npm package `bootstrap@5.3.8`, folder `dist/css` and `dist/js`)
- Create: `Quartermaster.Blazor/wwwroot/icons/bootstrap-icons-1.13.2/` (from `bootstrap-icons@1.13.2`, folder `font`)
- Delete: the 5.3.3 and 1.11.3 folders
- Modify: `Quartermaster.Blazor/wwwroot/index.html` (both links), any other file that names the old folders (search for `bootstrap-5.3.3` and `bootstrap-icons-1.11.3`)

- [ ] **Step 1:** Download both tarballs from `registry.npmjs.org`, unpack into a fresh scratch folder, copy only the folders above into place keeping the old layout.
- [ ] **Step 2:** Replace every reference to the old folder names.
- [ ] **Step 3:** Build and restart the dev server; `curl` both new CSS files and the icon font: HTTP 200.
- [ ] **Step 4:** Run `OpenSourceLibrariesTests`: PASS.

### Task 2: Rule test for Razor files

**Files:**
- Create: `Quartermaster.Server.Tests/Texts/UiConventionsTests.cs`

**Interfaces:**
- Produces: `UiConventionsTests.ConvertedDirectories` (a `string[]` of paths relative to `Quartermaster.Blazor`, grown by Tasks 8 to 10) and `UiConventionsTests.AllowedInlineStyles` (a `string[]` of `file:style` entries).

- [ ] **Step 1:** Write `Converted_pages_follow_the_ui_conventions`: for every `.razor` file under `ConvertedDirectories`, report each line containing `btn-hover-scale1`, `btn-outline-primary` or `style="` (unless the `file:style` pair is in `AllowedInlineStyles`), as `file:line: match`; `Assert.Fail` with the list when non-empty. Start with `ConvertedDirectories = ["Components", "Layout"]`.
- [ ] **Step 2:** Run it: FAIL, listing the current hits in `Components` and `Layout`.
- [ ] **Step 3:** It turns green at the end of Task 7; leave it red until then.

### Task 3: AsyncButton

**Files:**
- Create: `Quartermaster.Blazor/Components/AsyncButtonState.cs`, `Quartermaster.Blazor/Components/AsyncButton.razor`, `AsyncButton.razor.cs`, `Quartermaster.Blazor/Components/ButtonKind.cs`
- Modify: `Quartermaster.Blazor/wwwroot/css/app.css` (the stacked-label rule)
- Test: `Quartermaster.Server.Tests/Components/AsyncButtonStateTests.cs`

**Interfaces:**
- Produces: `public enum ButtonKind { Primary, Secondary, Danger }`; `public enum AsyncButtonPhase { Ready, Busy, Done, Failed }`; `public class AsyncButtonState` with `AsyncButtonPhase Phase`, `bool TryStart()`, `void Finish(bool succeeded)`, `void Reset()`, `void Dispose()` (after which `Reset` and `Finish` leave the phase unchanged), `static readonly TimeSpan ResetDelay = TimeSpan.FromSeconds(2)`.
- Produces: `<AsyncButton Text Icon Kind Action DoneText Small Disabled />` with `[Parameter, EditorRequired] string Text`, `string Icon` (Bootstrap Icons name without `bi-`), `ButtonKind Kind = ButtonKind.Primary`, `[EditorRequired] Func<Task<bool>> Action`, `string? DoneText` (defaults to `ui.async_button.done` "Gespeichert"), `bool Small`, `bool Disabled`.

- [ ] **Step 1:** Write tests: `Starts_ready`; `A_start_makes_it_busy_and_a_second_start_is_refused`; `A_success_shows_done_and_a_failure_failed`; `Reset_returns_to_ready`; `After_disposal_finish_and_reset_change_nothing`.
- [ ] **Step 2:** Run `AsyncButtonStateTests`: FAIL (type missing).
- [ ] **Step 3:** Implement the state class, then the component: busy shows `spinner-border spinner-border-sm`, done `btn-success` with `bi-check-lg` and the done text, failed `btn-danger` with `bi-x-lg` and `ui.async_button.failed` "Fehler"; an exception from `Action` counts as failed and is written with `Console.Error.WriteLine` including the exception; after `ResetDelay` (cancelled on dispose) back to ready; the three labels sit stacked in one `span class="async-button-label"` so the width stays; `aria-live="polite"` on the label. CSS in `app.css`: `.async-button-label { display: inline-grid; } .async-button-label > span { grid-area: 1 / 1; } .async-button-label > span[aria-hidden="true"] { visibility: hidden; }`.
- [ ] **Step 4:** Add `ui.async_button.done`, `ui.async_button.failed` to `I18nKey`, `de.json` ("Gespeichert", "Fehler") and `en.json` ("Saved", "Failed").
- [ ] **Step 5:** Run `AsyncButtonStateTests`: PASS; build clean.

### Task 4: Voting rights in the session

**Files:**
- Modify: `Quartermaster.Api/Users/LoginResponse.cs` (`LoginUserInfo`), `Quartermaster.Data/Users/UserExtensions.cs`, `Quartermaster.Server/Users/SessionEndpoint.cs`, `LoginEndpoint.cs`, `UserSettingsEndpoint.cs`
- Test: `Quartermaster.Server.Tests/Integration/Users/SessionVotingRightsTests.cs`

**Interfaces:**
- Produces: `LoginUserInfo.HasVotingRights` (`bool?`, null without linked member) and `LoginUserInfo.OpenFeeTotal` (`decimal?`); `LoginUserInfo ToLoginUserInfo(Member? member)` in the `UserExtensions` extension block, used by all three endpoints instead of their inline mapping.

- [ ] **Step 1:** Write `Session_carries_the_voting_rights_of_the_linked_member` (member with voting rights: true; with `OpenFeeTotal = 24`: 24) and `Session_without_member_has_no_voting_rights` (null).
- [ ] **Step 2:** Run: FAIL.
- [ ] **Step 3:** Implement; the endpoints look the member up by `Members.UserId`.
- [ ] **Step 4:** Run the new tests plus `UserSettingsEndpointTests`, `LoginEndpointTests`: PASS.

### Task 5: Account menu

**Files:**
- Modify: `Quartermaster.Blazor/Layout/MainNavBar.razor`, `MainNavBar.razor.cs`, `I18nKey.cs`, `de.json`, `en.json`

**Interfaces:**
- Consumes: `LoginUserInfo.HasVotingRights`, `OpenFeeTotal` (Task 4).

- [ ] **Step 1:** Logged in, replace the six right-hand items with one `nav-item dropdown`: toggle shows `bi-person-circle`, the display name and, when `HasVotingRights == true`, `bi-check-circle-fill text-success` with `title` "Stimmberechtigt"; menu: a non-clickable first line "Stimmberechtigt" or "Nicht stimmberechtigt" (plus "{amount} € offen" when `OpenFeeTotal > 0`), only when `HasVotingRights != null`; divider; Einstellungen (`/Settings`), Sitzungen (`/Sessions`), Benachrichtigungen (`/Account/Notifications`); divider; Sprache (the existing language choices as items); Hell/Dunkel (the existing toggle); divider; Abmelden. Reuse existing i18n keys where they exist; new keys under `ui.main_nav_bar.*` in both files.
- [ ] **Step 2:** Logged out, keep the login button and the language and theme switches.
- [ ] **Step 3:** Build, restart; run the audit for `/` logged in at 1280 px: `scrollWidth == clientWidth`.

### Task 6: PageHeader, StatusBadge, ResponsiveList

**Files:**
- Modify: `Quartermaster.Blazor/Components/PageHeader.razor`, `PageHeader.razor.cs`
- Create: `Quartermaster.Blazor/Components/StatusBadge.razor`, `StatusBadge.razor.cs`, `StatusTone.cs`, `ResponsiveList.razor`, `ResponsiveList.razor.cs`
- Modify: `EventStatusBadge`, `EventVisibilityBadge`, `MeetingStatusBadge`, `MeetingVisibilityBadge`, `MotionApprovalBadge`, `AssemblyMotions/AssemblyMotionStatusBadge`, `ChapterMarkBadges` (render through `StatusBadge`)

**Interfaces:**
- Produces: `PageHeader` parameters `Title` (required), `string? BackHref`, `string? BackText`, `RenderFragment? Subtitle`, `RenderFragment? Actions`; renders `PageBackLink` when `BackHref` is set, `h3` title, `Subtitle` in a `text-secondary` line that wraps, `Actions` in `d-flex flex-wrap gap-2`, title and actions in `d-flex flex-wrap justify-content-between align-items-start gap-2 mb-4`.
- Produces: `public enum StatusTone { Success, Warning, Danger, Neutral, Info }`; `<StatusBadge Tone Text />` rendering `badge rounded-pill border` plus `bg-{c}-subtle text-{c}-emphasis border-{c}-subtle` with c = success, warning, danger, secondary, info.
- Produces: `ResponsiveList<TItem>` with `IReadOnlyList<TItem> Items`, `RenderFragment Header` (the `thead` row), `RenderFragment<TItem> Row` (a `tr`), `RenderFragment<TItem> Stacked` (the content of one `list-group-item`); renders the table in `d-none d-md-block` and a `list-group d-md-none`.

- [ ] **Step 1:** Implement the three; tone mapping for the existing badges: done or accepted Success, open or waiting Warning, refused Danger, archived or withdrawn or draft Neutral, information (visibility, "Öffentlich") Info.
- [ ] **Step 2:** Build clean; restart; existing badge pages render (screenshot `/Administration/Events` desktop).

### Task 7: Conventions page and foundation clean-up

**Files:**
- Create: `Quartermaster.Documentation/UiConventions.md`
- Modify: everything under `Quartermaster.Blazor/Components` and `Quartermaster.Blazor/Layout` that the rule test reports; remove `.btn-hover-scale1` from the CSS files

- [ ] **Step 1:** Write the page (English, per CLAUDE.md docs rule): page header, buttons, AsyncButton, sections, tables and ResponsiveList, key-value blocks, forms, badges and tones, feedback, colours. Each with the classes from Global Constraints and one short Razor example.
- [ ] **Step 2:** Convert the shared components and layout against it (`DeleteButton` to normal size by default, `PageBackLink` unchanged apart from rules).
- [ ] **Step 3:** Run `UiConventionsTests`: PASS.

### Task 8: Member and public pages

**Files:**
- Modify: `Pages/Home.razor`, `Pages/Events/`, `Pages/AssemblyMotions/`, `Components/AssemblyMotions/`, `Pages/Forms/`, `Pages/MembershipApplication/`, `Pages/DueSelector/`, `Pages/Motions/`, `Pages/SupportSignatures/`, `Pages/UserSettings.razor`, `Pages/UserNotificationPreferences.razor`, `Pages/UserSessions.razor`, `Pages/VersionHistory.razor`, `Pages/Attributions/`, `Pages/ConfirmSubmission.razor`, `Pages/Login*.razor` (with their `.razor.cs`)
- Modify: `UiConventionsTests.ConvertedDirectories` (add these paths)

- [ ] **Step 1:** Per page, in this order: `PageHeader` (back link, subtitle, actions); buttons per rule; `AsyncButton` for every server action, removing its success toast and keeping its error toast; sections as cards; member-facing lists through `ResponsiveList` (portal lists, collecting, mine, ranking, events, my forms); key-value blocks as `dl`; forms per rule; badges through the badge components; empty lists through `EmptyState`, loading through `LoadingSpinner` centred in its section; inline styles out; rows wrapping.
- [ ] **Step 2:** Add the paths to `ConvertedDirectories`; run `UiConventionsTests`: PASS.
- [ ] **Step 3:** Run the groups `Quartermaster.Server.Tests.AssemblyMotions`, `.Integration.AssemblyMotions`, `.PublicForms`, `.Integration.Users`, `.Texts`: PASS.
- [ ] **Step 4:** Rerun the audit for this group's routes: no sideways scrolling at 390 and 1280 px.

### Task 9: Board work

**Files:**
- Modify: under `Pages/Administration/`: `MembershipApplication*`, `DueSelection*`, `Member*`, `Motion*`, `Meeting*`, `Event*`, `Mailings/`, `Chapter*`, `Forwarding/`, `ApplicantRegister/`, `AssemblyMotions/`, `SupportSignatures/`; and `Components/Events/`, `Components/Meetings/`, `Components/Forwarding/`
- Modify: `UiConventionsTests.ConvertedDirectories`

- [ ] **Step 1:** Same per-page order as Task 8 (admin lists stay tables in `table-responsive`).
- [ ] **Step 2:** Add the paths; `UiConventionsTests`: PASS.
- [ ] **Step 3:** Run `.Integration.Events`, `.Integration.Meetings`, `.Integration.Motions`, `.Integration.Admin`, `.Integration.Members`, `.Forwarding`, `.Integration.Forwarding`: PASS.
- [ ] **Step 4:** Audit for this group: no sideways scrolling at 390 and 1280 px.

### Task 10: Administration, system and assembly console

**Files:**
- Modify: the remaining `Pages/Administration/` pages (settings, setup assistants, templates, PDF templates, roles, users, news posts, divisions, accreditation, import status, login lockouts, notification stats, travel expense rules), `Pages/AssemblyAssistant/` except the overlay page, remaining `Components/`
- Modify: `UiConventionsTests.ConvertedDirectories` becomes `["."]` (the whole Blazor project), with the overlay page and the genuinely needed inline styles in `AllowedInlineStyles`

- [ ] **Step 1:** Same per-page order as Task 8.
- [ ] **Step 2:** `UiConventionsTests` over the whole project: PASS.
- [ ] **Step 3:** Run `.Integration.Settings`, `.Settings`, `.Integration.Roles`, `.Integration.AssemblyAssistant`, `.AssemblyAssistant`, `.NewsPosts`, `.Accreditation`: PASS.

### Task 11: Acceptance

**Files:**
- Modify: `Quartermaster.Server/Changelog/V9.2.md` (create), `todo.md` (mark the UI review item done)

- [ ] **Step 1:** Full audit rerun (all routes, phone and desktop, dark subset, logged out): no member or board page scrolls sideways at 390 or 1280 px; look at the screenshots of every converted page, light and dark.
- [ ] **Step 2:** Changelog section "Oberfläche": consistent pages, account menu with voting rights, buttons that show saving, phone layout.
- [ ] **Step 3:** Run the groups from Tasks 8 to 10 once more plus `.Texts` and `.Components`: PASS; build clean; dev server restarts without exceptions.
- [ ] **Step 4:** Whole-branch review by a fresh reviewer.
