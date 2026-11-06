# UI conventions

How Quartermaster pages are built. Every page follows these rules; `UiConventionsTests` fails on the patterns that are no longer used (`btn-hover-scale1`, `btn-outline-primary`, inline `style=` outside its allow list, icon names Bootstrap Icons does not know). Use Bootstrap 5.3 classes wherever possible; customise through the CSS variables in `piratenpartei-theme.css`, not through own CSS.

## Page header

Every page starts with `PageHeader`: optional back link, title, an optional line below the title (chapter, date, status badges) and the page's actions. On a phone the actions wrap below the title.

Buttons that only lead to a linked page (the other side of the same record, such as an event's public page and its management page, or a motion and its membership application) go into the back link's row on the right, as small outline buttons: `Links` on `PageHeader` or on `PageBackLink`. Each shows only when the viewer may open that page. The page's own actions stay next to the title.

```razor
<PageHeader Title="@Event.PublicName" BackHref="/Administration/Events">
    <Subtitle><EventStatusBadge Status="@Event.Status" /> @Event.ChapterName</Subtitle>
    <Actions>
        <AsyncButton Text="@I18n[I18nKey.Ui.Common.Save]" Icon="check-lg" Action="SaveAsync" />
        <a class="btn btn-outline-secondary" href="/Events/@Id"><i class="bi bi-eye"></i> @I18n[...]</a>
    </Actions>
</PageHeader>
```

## Buttons

- Main action: `btn btn-primary`, at most one per page header and one per form.
- Any other action: `btn btn-outline-secondary`.
- Destructive action: `btn btn-outline-danger` (or `DeleteButton`); filled `btn-danger` only for the final confirmation in a dialog.
- Normal size in page headers, forms and cards; `btn-sm` only inside table rows and lists.
- Always an icon (Bootstrap Icons) plus text. Icon-only buttons only inside table rows, with `title` and `aria-label`.
- No `btn-outline-primary`, no hover zoom.
- A row of buttons is `d-flex flex-wrap gap-2`, so it wraps instead of pushing the page sideways. Buttons in a table cell sit in `d-flex gap-1` (`justify-content-end` in a right-aligned cell). Never rely on the space between two buttons: Blazor drops it around `@if` blocks and they touch. Buttons only touch inside a `btn-group` or `input-group`.
- Links that read as links stay links (`<a>` without `btn`).

## AsyncButton

For every action that waits on the server: save, check, send, checklist actions. Not for navigation or opening a dialog.

```razor
<AsyncButton Text="@I18n[I18nKey.Ui.Common.Save]" Icon="check-lg" Kind="ButtonKind.Primary" Action="SaveAsync" />
```

`SaveAsync` returns `Task<bool>`: true on success, false on failure after showing the error toast with the server's reason (`ToastService.ErrorAsync(response)`). The button shows a spinner while busy, then "Gespeichert" (or `DoneText`) in green or "Fehler" in red for two seconds. Do not show a success toast as well.

## Sections

Every section of a page is a card, `card mb-4`, its title an `h5` in the `card-body`, actions on the right of the title in a wrapping row. No nested cards; a section inside a section is a heading inside the same card. No inline styles.

```razor
<div class="card mb-4">
    <div class="card-body">
        <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
            <h5 class="mb-0">@I18n[...]</h5>
            <button class="btn btn-sm btn-outline-secondary">...</button>
        </div>
        ...
    </div>
</div>
```

## Tables and lists

- Every table is `table table-hover align-middle` (`table-sm` only for dense admin lists) inside `table-responsive`. Actions in the last column; numbers and dates right-aligned (`text-end`).
- Member-facing lists (motion portal lists, my motions, motions looking for support, events, my forms) use `ResponsiveList`: a table from medium width, one stacked row per entry on a phone.
- Admin lists stay tables and scroll inside their card on a phone.

```razor
<ResponsiveList Items="@Items">
    <Header><tr><th>@I18n[...Title]</th><th>@I18n[...Status]</th></tr></Header>
    <Row Context="item"><tr><td><a href="...">@item.Title</a></td><td><AssemblyMotionStatusBadge Status="@item.Status" /></td></tr></Row>
    <Stacked Context="item">
        <a class="fw-semibold" href="...">@item.Title</a>
        <div class="d-flex flex-wrap gap-2 small text-secondary mt-1">@item.NumberLabel <AssemblyMotionStatusBadge Status="@item.Status" /></div>
    </Stacked>
</ResponsiveList>
```

## Key-value blocks

A definition list: side by side from small width, stacked on a phone.

```razor
<dl class="row mb-0">
    <dt class="col-sm-4">@I18n[...]</dt>
    <dd class="col-sm-8">@value</dd>
</dl>
```

## Forms

- Label above the field (`form-label`), help text below (`form-text`).
- Full width on a phone; short fields share a row on desktop: `row g-3` with `col-md-*`.
- Read-only fields get both `readonly` and `disabled`.
- The form's buttons sit at the bottom of its card, main action first, in a wrapping row.
- Date and time fields keep the browser pickers.

## Status badges

Every status renders through `StatusBadge` (soft colours, normal size), usually via a typed badge component such as `EventStatusBadge`. Tones:

- `Success`: done or accepted.
- `Warning`: open or waiting (pending, collecting support, in revision, in progress).
- `Danger`: refused (rejected, formally insufficient).
- `Neutral`: draft, archived, withdrawn, closed.
- `Info`: in progress without waiting (active, scheduled, submitted) and information such as visibility.

## Feedback

- Loading: `LoadingSpinner`, centred in the section that loads.
- Empty lists: `EmptyState`.
- Errors and notices concerning a section: a Bootstrap alert at the top of that section.
- Toasts: errors, and actions without an `AsyncButton`.

## Colours

Orange (the primary colour) for main buttons, links, the active menu entry and focus rings; not for badges or outline buttons. No inline colours. Dark mode works through Bootstrap's variables; never hard-code a colour that only fits one mode.
