# UI review and modernisation of the whole application

Design agreed with the user on 2026-11-05 and 2026-11-06. Ships as one update (V9.2 or V10, decided later) on a separate branch.

## Goal

Quartermaster feels like one application: the same thing looks and behaves the same on every page, and the pages members and boards use work on a phone. The look becomes calmer and more current without forcing the party orange into places where it does not fit.

What the user said:

- Consistency comes first; inconsistencies are the main problem.
- Phone use matters for members (public pages, motion portal, forms) and for boards (board work). Administration and system pages are used on desktop; on a phone they may look rough (the template editor will never work well there), but no page may scroll sideways.
- The orange stays where it fits naturally; the design is not built around it.
- Stay on Bootstrap and Bootstrap Icons, update them, use Bootstrap classes wherever possible, customise through CSS variables, add as little own CSS as possible.

Assumptions taken as given: light and dark mode both stay; the menus and page structure stay, the pages are cleaned up rather than redesigned.

Success:

- No member or board page scrolls sideways at 390 px or 1280 px width, logged in or not, in light or dark mode.
- Every page uses the shared page header, buttons follow one rule, lists follow one table pattern, statuses one badge.
- No `btn-hover-scale1`, no `btn-outline-primary`, no inline `style=` in Razor files beyond a short allow list.

## Audit (2026-11-05)

Headless screenshots of 97 pages logged in at 390 px and 1280 px (17 also in dark mode) and 26 public pages logged out, on the dev database with test data. Findings:

- Logged in, the top menu is about 1400 px wide: at 1280 px, 110 of 140 logged-in screenshots scroll sideways and the language and theme switches sit off screen. The menu collapses only below 992 px.
- Eight pages scroll sideways on a phone (motion dashboard, role detail, template editor, certification offices, due selection detail, board motion detail, event templates): button and badge rows that do not wrap.
- Tables inside cards are cut off on a phone (motion portal deadlines, version history).
- Many buttons are under 32 px high on a phone (template editor 24, event page 20).
- Page titles are built three ways (`PageHeader` on 16 pages, plain `h3` on 67, `h2` on 8).
- "Save" appears in six button styles, "Delete" in five; button rows mix filled orange, orange outline and grey outline; the event page has an icon-only save square that looks disabled; 33 buttons zoom on hover.
- At least eight table class combinations; cards with `mb-3`, `mb-4` or no spacing; 152 inline styles.

Three bugs found on the way are separate todo items (meeting detail date format crash, raw `<code>` in the event description hint, home page spinner on a phone).

## Foundation

### Versions

Bootstrap 5.3.3 to 5.3.8 and Bootstrap Icons 1.11.3 to 1.13.2, both vendored under `Quartermaster.Blazor/wwwroot/css/` as today.

### Menu

- Logged in, the six separate items on the right of the top menu (name, sessions shield, notifications bell, "Abmelden", language, theme) become one account menu: the user's display name as a dropdown with Einstellungen, Sitzungen, Benachrichtigungen, Sprache, Hell/Dunkel and Abmelden. The menu then fits from about 1000 px and the switch to the phone menu at 992 px works again.
- The account menu shows the voting rights: a green `bi-check-circle-fill` next to the name with the tooltip "Stimmberechtigt" when the member has voting rights, nothing otherwise; the opened menu starts with "Stimmberechtigt" or "Nicht stimmberechtigt" and the open amount when there is one, as on the settings page. Only accounts linked to a member show it. The session (`LoginUserInfo`, filled by the session and login endpoints) gets the voting rights and the open amount.
- Logged out, the right side keeps the login button and the language and theme switches.
- The main menus stay as they are.

### Page header

Every page uses `PageHeader`, extended for: an optional back link, the title, an optional line below it (chapter, date, status badges), and the page's actions. On desktop the actions sit right of the title, on a phone they wrap below it. The page title elements (`h2`, `h3` as page titles) go.

### Buttons

- Main action: `btn-primary` (filled orange), at most one per page header and one per form.
- Other actions: `btn-outline-secondary`. The orange outline (`btn-outline-primary`) goes.
- Destructive: `btn-outline-danger`; filled `btn-danger` only for the final confirmation in a dialog.
- Links that read as links stay links.
- Normal size in page headers, forms and cards; `btn-sm` only inside table rows and lists.
- Always icon plus text; icon-only only inside table rows, with a tooltip and an accessible label.
- No hover zoom: `btn-hover-scale1` and its CSS go.
- Rows of buttons always wrap: `d-flex flex-wrap gap-2`.

### AsyncButton

A shared component for every action that waits on the server (save, check, send, checklist actions), not for navigation or opening a dialog.

- Ready: a button of the chosen kind (main, other, destructive) with icon and text.
- Busy: spinner, same text, disabled.
- Done: `btn-success`, check icon, "Gespeichert" (or a text the caller passes, such as "Gesendet"). No success toast.
- Failed: `btn-danger`, cross icon, "Fehler"; the page still shows the error toast with the server's reason.
- After about two seconds back to ready.
- The button keeps the width of its widest state; this needs the only few lines of own CSS the foundation adds.
- The state change is announced to screen readers (`aria-live`).
- The page passes its action as a function returning success or failure. The state sequence lives in a plain C# class covered by unit tests.

### Cards and sections

Every section of a page is a card, its title a heading in the card body (`h5`) with actions on the right that wrap below on a phone. Cards are `mb-4` apart, without inline styles, without nesting.

### Tables and lists

- One standard: `table table-hover align-middle`; `table-sm` only for dense admin lists; actions in the last column; numbers and dates right-aligned.
- Member-facing lists (motion portal lists, my motions, motions looking for support, events, my forms) use a shared `ResponsiveList`: a Bootstrap table from `md` width, a Bootstrap `list-group` with one stacked row per entry below it.
- Admin lists stay tables in `table-responsive`, scrolling inside their card on a phone.
- Key-value blocks use a definition list (`dl` with `row`, `dt`, `dd`): side by side on desktop, stacked on a phone.

### Forms

Labels above the field, help text below (`form-text`); full width on a phone, short fields sharing a row on desktop (`row g-3` with `col-md-*`); read-only fields `readonly` plus `disabled`; the form's buttons at the bottom of its card, main action first, wrapping on a phone; date and time fields keep the browser pickers.

### Status badges

A shared `StatusBadge` in Bootstrap's soft colours (`text-bg-*` style with the `*-subtle` variables), normal text size. Green for done or accepted, yellow for open or waiting, red for refused, grey for archived or withdrawn, blue for information such as "Öffentlich". The existing status badges (events, meetings, board motions, motion portal) use it.

### Feedback

One `LoadingSpinner`, centred in the loading section; `EmptyState` for every empty list; Bootstrap alerts at the top of the section they concern; toasts for errors and for actions without an `AsyncButton`.

### Colours and dark mode

Customisation only through CSS variables in `piratenpartei-theme.css` (`--bs-*` on `:root` and on component classes). Orange for main buttons, links, the active menu entry and focus rings; not for badges or outline buttons. Inline colours and styles go, replaced by Bootstrap classes.

### Conventions page

`Quartermaster.Documentation/UiConventions.md` says which component and which classes to use when: page header, buttons, AsyncButton, cards, tables and ResponsiveList, forms, badges, feedback.

## Rollout

All four phases in one update, in this order:

1. Foundation: versions, account menu with voting rights, `PageHeader`, `AsyncButton`, `StatusBadge`, `ResponsiveList`, conventions page, rule test.
2. Member and public pages: home, events, the motion portal pages including motion detail and editor, forms and the membership application, the due form, settings, notifications and sessions, the public signature page.
3. Board work: membership applications and due selections, members, board motions and meetings, event management, mailings, chapters, forwarding.
4. Administration and system: settings, setup assistants, templates, roles, users, PDF templates, social media, divisions, the assembly console. The OBS overlay (`/assembly/{id}/overlay`) is out of scope.

## Checks

- The screenshot audit (script kept outside the repository for now) is rerun after each phase; a phase is done when no member or board page scrolls sideways at 390 px or 1280 px and the changed pages' screenshots were looked at.
- A rule test fails on `btn-hover-scale1`, `btn-outline-primary` and inline `style=` in Razor files, with a short allow list for styles that are genuinely needed (for example computed widths); it covers every page once phase 4 is done.
- Unit tests for the `AsyncButton` state class and for the voting rights in the session.
- Existing tests stay green; texts follow the generic masculine rule (`GermanPersonNounTests`).

## Out of scope

- The OBS overlay.
- The three bugs from the audit (own todo items).
- Making admin pages pleasant on a phone beyond not scrolling sideways.
- New features or changed page content; texts change only where a button or heading changes.
