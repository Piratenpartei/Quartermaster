# Bundesbank Bankleitzahlendatei

`blz-aktuell.txt` is a snapshot of the Bundesbank's publicly published
Bankleitzahlendatei (BLZ -> BIC mapping for German payment service
providers). It is used at PDF render time to look up the BIC for an IBAN
the user typed (`BankCodeBicLookupService`), so people don't have to enter
their BIC by hand.

## Source

- Download page: https://www.bundesbank.de/de/aufgaben/unbarer-zahlungsverkehr/serviceangebot/bankleitzahlen/download-bankleitzahlen-602592
- Direct URL of the snapshot in this repo: https://www.bundesbank.de/resource/blob/602632/cfc62adaf6c1f09b1b4ec0e59a255b91/472B63F073F071307366337C94F8C870/blz-aktuell-txt-data.txt
- Snapshot taken: 2026-06-05
- Snapshot validity: 09.03.2026 - 07.06.2026

The Bundesbank ships a new file every quarter (~ three to four weeks
ahead of each validity date). A backlog item under `todo.md / Operations`
captures the plan to swap manual refresh for a background service.

## Format

ASCII fixed-width, ISO-8859-1 (Latin-1), CRLF line endings, ~14k records.
Field layout is specified in the Merkblatt linked from the download page.
Relevant columns for our usage:

| Field | Positions | Length | Meaning                              |
|-------|-----------|--------|--------------------------------------|
| 1     | 1-8       | 8      | Bankleitzahl                          |
| 2     | 9         | 1      | "1" = bankleitzahlfuehrend ("primary") |
| 8     | 140-150   | 11     | Business Identifier Code (BIC)        |

We only consume rows with field 2 = "1" (primary record per BLZ).

## Licence and attribution

The Bundesbank's general terms of use
(https://www.bundesbank.de/de/startseite/benutzerhinweise/nutzungsbedingungen-fuer-den-allgemeinen-gebrauch-der-website-763554)
permit storing, redistributing, and reproducing this data for personal
or business use, provided the attribution `Quelle: Deutsche Bundesbank`
is shown. Quartermaster surfaces that attribution next to the IBAN input
on the Auslagenerstattung wizard. The Bundesbank disclaims accuracy of
the public web version ("unverbindlich"), so the lookup is best-effort -
the user can override the auto-filled BIC before signing the printed
form.
