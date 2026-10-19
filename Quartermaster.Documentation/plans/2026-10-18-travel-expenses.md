# Travel Expenses (V6.0)

A travel expense reimbursement form ("Reisekostenerstattung") on top of `FormWizard`, replacing the standalone tool at reisekosten.piratenpartei.de. The rules (rates, allowances, header, rules text) are data per chapter; the PDF reproduces the official forms from the Piratenwiki through the PDF template engine; logged-in users get server drafts and a receipt cache. Download only, no submission pipeline yet.

## Legal basis

The federal travel expense rules (RKO of 15.02.2024, valid from 01.01.2024) refer to the EStG for mileage and meal allowance:

- mileage (§ 9 Abs. 1 Satz 3 Nr. 4a EStG with § 5 BRKG): car 0.30 EUR/km, other motor vehicles 0.20 EUR/km; chapters may set their own rates
- meal allowance (§ 9 Abs. 4a EStG), per calendar day of absence from home: 28 EUR for 24 hours, 14 EUR each for arrival and departure day of a trip with an overnight stay, 14 EUR for a day trip of more than 8 hours
- meals provided by the party or an organiser reduce the day's allowance by 20 % (breakfast) or 40 % (lunch, dinner) of the full-day rate, never below zero
- accommodation by receipt minus breakfast (itemised amount, otherwise 20 % of the full-day rate), or the flat rate per night of the rules (20 EUR)
- Germany only; trips abroad are out of scope

Deadlines (8 weeks, 28 February of the following year) never block: they appear as notes, and the chapter decides.

## Rules data

`TravelExpenseRuleSet` (chapter, valid from) resolved for the trip's start date, walking up the chapter tree to the federal rules:

- car rate steps (`UpToKm`, `RatePerKm`; BW: 0.30 up to 20 km, 0.35 beyond), optional motorcycle rate steps
- meal allowance: day trip over 8 hours, arrival/departure day, full day; reduction percentages for breakfast, lunch, dinner
- accommodation flat rate per night
- submission deadline in weeks, cut-off day and month in the following year
- form header (organisation line, address, treasurer email), form version label, rules text (page 2), PDF template key

Seeds from the wiki forms: federal party J2.0 (car 0.30, motorcycle 0.20), Bayern 2.0-LV-BY from 01.04.2022 (0.30 / 0.13), Baden-Württemberg BW1.0 from 09.06.2021 (0.30 up to 20 km, then 0.35; no motorcycle), Niedersachsen J1.1 from 01.03.2022 (0.30 / 0.13). Hessen uses the federal rules.

Editing: `/Administration/TravelExpenseRules`, chapter-scoped permission `travel_expense_rules_edit`. A "Reisekosten" settings assistant holds the receipt options and links to the rules page.

## Calculator

`TravelExpenseCalculator` in `Quartermaster.Api`, pure, used by the browser (live amounts, anonymous PDF input) and the server (PDF). Input: rules plus the trip; output: meal allowance per calendar day with reductions, mileage per vehicle and rate step, tickets, accommodation, other costs, total, donation, payout, and the notes list (missing resolution, modules outside start/end, overlapping legs, car leg without km, deadline passed, no costs).

## Wizard (`/Forms/TravelExpense`)

1. Person: name, member number, email, address, membership chapter (prefilled when logged in)
2. Federation travelled for and function
3. Trip: purpose, destination, resolution number and date
4. Trip builder
5. Donation and bank details (IBAN check; BIC optional, no bank code database)
6. Overview: amounts, notes, PDF

### Trip builder

A vertical timeline. Top: start (place, defaults to the home address; date and time). Bottom: end (place, date and time). Start and end are the absence from home the meal allowance is computed from. Modules in between, any number, mixed, sorted by time:

- vehicle leg: car or motorcycle (only when the rules have a rate), from, to, departure, km, route receipts
- transit leg: train, bus, plane, other public transport; from, to, departure, ticket price, receipts
- hotel stay: check-in, check-out (nights computed), by receipt (total, breakfast included per night with the itemised amount) or flat rate, receipts
- provided meal: day plus breakfast, lunch or dinner, paid by the party or an organiser
- other cost: description, amount, receipts (parking, entrance, taxi, local transport)

Each module shows its amount; the builder shows the running total and the notes.

## Storage

- logged in: `TravelExpenseReimbursements` (owner, status, travel chapter, start, total, created/updated/submitted, the input as JSON), list/copy/delete like the expense form, listed on `/Forms/Mine`
- anonymous: browser storage via the existing local draft envelope, no receipts (attachment count typed in), PDF via an anonymous render endpoint

## Receipt cache (`/Forms/Receipts`)

`UserReceipts` (owner, label, file name, content type, size, uploaded) with files under `data/receipts/{userId}/`. JPEG, PNG and PDF, checked by content. Options `forms.receipts.max_per_user` (100) and `forms.receipts.max_file_mb` (10); global permission `receipts_unlimited` lifts the count limit. Receipts stay until the user deletes them. Modules reference receipts by id; a picker uploads or selects.

## PDF

- the template engine accepts a `<document>` root with several `<page>` children
- default template `travel-expense.default` reproduces the official layout (federal, BY, BW, NDS) with header, version and rates from the rules: page 1 the form, page 2 the rules text, page 3 the "Reiseverlauf" appendix with every module and calculation; more than three other costs go into the appendix with "siehe Anlage" on the form
- receipts follow: images as one page each scaled to A4, PDFs merged with `DocumentOperation.MergeFile`

## Phases

1. (built) Rules: entities, repository, seeds, resolution up the tree, calculator with tests
2. (built) Receipt cache: storage, endpoints, limits, permission, page, tests
3. (built) Server drafts and endpoints (create, update, get, list, copy, delete, render, pdf), anonymous render, tests
4. (built) PDF: multi-page templates, default template, appendix, receipt merge, tests
5. (built) Wizard and trip builder, local drafts, `/Forms/Mine`, catalog entry
6. (built) Rules admin page, settings assistant, docs, changelog `V6.0.md`

One migration (`M006`) for V6.0.
