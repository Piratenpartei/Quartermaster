# Term Translations (German ↔ English)

This document maps German UI terms to their English equivalents for developer reference.
The frontend display language is German; all code (identifiers, comments, APIs) is English.

## Navigation & Structure

| German | English | Notes |
|---|---|---|
| Mitgliedsportal | Member Portal | |
| Verwaltung | Administration | |
| Gemeindedatenbank | Administrative Division Database | Gemeinde = municipality/administrative division, Datenbank = database |
| Beitragseinstufung | Dues Classification | The process of selecting/classifying membership dues |
| Zusammenfassung | Summary | |
| Quellen & Lizenzen | Sources & licenses | Attribution page for data, fonts and open source libraries |

## Dues Selection (Beitragseinstufung)

| German | English | Notes |
|---|---|---|
| Beitrag | Dues / Membership Fee | |
| Jahresbeitrag | Annual Dues | |
| Monatsnettoeinkommen | Monthly Net Income | |
| Jahresnettoeinkommen | Annual Net Income | |
| Jahreseinkommen | Annual Income | |
| Einstufung nach Monatseinkommen | Classification by Monthly Income | |
| Einstufung auf 1% des Jahreseinkommens | Classification at 1% of Annual Income | |
| Geminderter Beitrag | Reduced Dues | |
| Jahreswunschbeitrag | Desired Annual Dues | |
| Begründung | Justification | |
| Dauerhaft | Permanent | |
| Empfohlen | Recommended | |

## Payment (Zahlungsweise)

| German | English | Notes |
|---|---|---|
| Zahlungsweise | Payment Schedule | |
| Zahlungsart | Payment Method | |
| Zahlungsplan | Payment Plan | |
| Lastschrift | Direct Debit | |
| Lastschriftmandat erteilen | Authorize Direct Debit Mandate | |
| Kontoinhaber | Account Holder | |
| Kontoangaben | Account Details | |
| Jährlich | Annual | |
| Quartalsweise | Quarterly | |
| Monatlich | Monthly | |

## User Data

| German | English | Notes |
|---|---|---|
| Vorname | First Name | |
| Nachname | Last Name | |
| E-Mail Adresse | Email Address | |
| Mitgliedsnummer | Member Number | |

## Administrative Divisions (Gemeinden)

| German | English | Notes |
|---|---|---|
| Gemeinde | Municipality / Administrative Division | Refers to the administrative unit, not just "community" |
| Gemeindedatenbank | Administrative Division Database | |

## General UI

| German | English | Notes |
|---|---|---|
| Weiter | Next / Continue | |
| Zurück | Back | |
| Überspringen | Skip | |
| Absenden | Submit | |
| Pflichtfeld | Required Field | |

## Membership Application (Mitgliedsantrag)

| German | English | Notes |
|---|---|---|
| Mitgliedsantrag | Membership Application | |
| Persönliche Daten | Personal Data | |
| Geburtsdatum | Date of Birth | |
| Staatsangehörigkeit | Citizenship | |
| Telefonnummer | Phone Number | |
| Adresse | Address | |
| Gliederung | Chapter | Organizational unit within the party |
| Zugeordnete Gliederung | Assigned Chapter | |
| Keycloak-Gliederung | Keycloak department | Value of the Keycloak attribute `ekklesia_department` that maps a member to a chapter |
| Straße | Street | |
| Hausnr. | House Number | Abbreviated in UI |
| PLZ | Post Code | Abbreviation of Postleitzahl |
| Ort | City / Place | |
| Erklärungen | Declarations | |
| Konformitätserklärung | Conformity Declaration | Acceptance of party principles and statutes |
| Eintritt zum | Entry Date | Date of joining |
| Mitgliedsantrag absenden | Submit Membership Application | |

## Administration

| German | English | Notes |
|---|---|---|
| Ausstehend | Pending | Application/due selection status |
| Genehmigt | Approved | |
| Abgelehnt | Rejected | |
| Automatisch genehmigt | Auto-Approved | Non-reduced dues |
| Schwebend | Floating | Applicant accepted by a chapter but first dues unpaid — no member number assigned yet. Distinct from `Ausstehend` (application status). |
| Anträge | Applications | |
| Einstufungen | Classifications / Selections | Due selections |
| Aktionen | Actions | |
| Eingereicht | Submitted | |
| Gewählter Beitrag | Selected Dues | |
| Geminderter Betrag | Reduced Amount | |
| Assistent | Assistant | One settings area under System settings |
| Erweiterte Einstellungen | Advanced settings | Full option list |
| Speichern und prüfen | Save and check | Saves a section, then tests the stored credentials |
| Eingerichtet / Teilweise / Nicht eingerichtet | Configured / Partial / Not configured | Status badge on a settings card |
| Erben | Inherit | Removes a chapter's own value |

## Motions (Anträge)

| German | English | Notes |
|---|---|---|
| Antrag | Motion | A formal proposal for chapter voting |
| Anträge | Motions | |
| Antrag einreichen | Submit Motion | |
| Antragsteller | Author / Proposer | |
| Antragstext | Motion Text | |
| Abstimmung | Vote / Voting | |
| Stimme | Vote (noun) | |
| Dafür | Approve / In Favor | |
| Dagegen | Deny / Against | |
| Enthaltung | Abstain | |
| Formal abgelehnt | Formally Rejected | Doesn't meet requirements |
| Ohne Beschluss geschlossen | Closed Without Action | |
| Umgesetzt | Realized / Implemented | |
| Öffentlich | Public | |
| Nicht öffentlich | Non-public | Contains personal data |
| Vorstandsmitglied | Board Member / Officer | |
| Vorsitzender | Chair | Captain |
| Stellv. Vorsitzender | Vice Chair | FirstOfficer |
| Quartiermeister | Quartermaster | Pirate-themed role |
| Schatzmeister | Treasurer | |
| Stellv. Schatzmeister | Vice Treasurer | ViceTreasurer |
| Pol. Geschäftsführer | Political Director | PoliticalDirector |
| Beisitzer | Board Member | Member (generic officer) |

## Motion Portal (Antragsportal)

| German (UI) | English (code) | Meaning |
|---|---|---|
| Versammlung | assembly | An `Event` with the motion portal enabled |
| Antragsportal | motion portal | The motion portal of an event |
| Antrag | assembly motion | A motion addressed to an assembly. Not the existing board `Motion` |
| Antragsart | motion type | GP, WP, SÄA, SBA, PP, SO or whatever the event configures |
| Antragsteller | author | The member who created the motion |
| Mitantragsteller | co-submitter | A member who joined the motion; counts toward the required number of submitters |
| Antragskommission (AKO) | motion commission | Users holding `assemblymotions_manage` for the event's chapter |
| Formprüfung | formal check | The commission's decision whether a motion is admitted |
| Rückfrage | inquiry | A question from the commission to the submitters with a response deadline |
| Konkurrenz | competition | Two motions that exclude each other |
| Priorisierung | prioritization | The optional upvote phase that determines the order of motions |
| Fassung | revision | One saved version of a motion; every change creates a new one |
| Versionsgeschichte | revision history | The list of revisions of a motion with a diff to the predecessor |
| Änderungsnotiz | change note | The mandatory explanation of a commission edit |
| konkurrierender Antrag | competing motion | A motion linked to an earlier submitted motion that excludes it; may have a later deadline |
| Reihenfolge | order | The ranking of motions by prioritization, public after the prioritization window |
| Ergebnis | result | The outcome of the assembly's vote on a motion, entered by the commission |

## Assembly Assistant (Versammlungsassistent)

| German | English | Notes |
|---|---|---|
| Versammlungsassistent | assembly assistant | Mumble bot, chair console and OBS overlay of a running assembly |
| Versammlungsleitung (VL) | chair | Holds `assembly_chair` for the event's chapter; also the name of the bot's channel |
| VL-Helfende | chair helper | Same permission as the chair, appointed by the chair (GO § 3 Abs. 2) |
| Wortmeldung | request | A member waiting in a request channel |
| Wortmeldekanal | request channel | Pro, Contra, Frage and GO-Antrag |
| Redner | speaker channel | Where the current speaker sits |
| Antragsteller | motion author / candidate channel | Where the motion author or the candidate of the current item sits; code `MotionAuthor` |
| Rederecht | give the floor | Move a member into the speaker or motion author channel |
| Fertig | done | Move the speaker back to the listener channel |
| Redeliste | list of speakers | Can be closed now or at a set time; later requests are marked |
| Redezeit | speaking time | Per-speech countdown, starts with the first voice packet |
| Durchgang | round | Up to five Pro and five Contra speeches |
| Überspringen | skip | The request stays in the queue but is not suggested |
| Abweisen | dismiss | The request is removed from the suggestions without moving the member |
| Einblendung | overlay | Web page used as an OBS browser source |

## Travel Expenses (Reisekostenerstattung)

| German | English | Notes |
|---|---|---|
| Reisekostenerstattung | travel expense reimbursement | The form at `/Forms/TravelExpense`; code `TravelExpense` |
| Reisekostenordnung (RKO) | travel expense rules | Rates, deadlines and rules text of a chapter; code `TravelExpenseRuleSet` |
| Fassung | version | One dated version of the rules; `VersionLabel` holds e.g. "J2.0" |
| Reiseverlauf | itinerary / trip builder | The timeline of the trip in the wizard and the PDF appendix |
| Fahrt mit eigenem Fahrzeug | vehicle leg | Car or motorcycle, reimbursed per kilometre; code `VehicleLeg` |
| Fahrkarte | ticket / transit leg | Train, bus, plane or other fare; code `TransitLeg` |
| Übernachtung | hotel stay | By invoice or at the flat rate; code `HotelStay` |
| Gestellte Mahlzeit | provided meal | Paid for by the party or the organiser, reduces the meal allowance; code `ProvidedMeal` |
| Sonstige Kosten | other cost | Code `OtherCost` |
| Verpflegungspauschale | meal allowance | § 9 Abs. 4a EStG |
| An- und Abreisetag | arrival and departure day | Day of a multi-day trip that is not a full day |
| Kilometerstaffel | mileage rate steps | Rates per km up to a limit, the last step open-ended; code `RateStepDTO` |
| Stichtag | cut-off date | Day in the following year after which applications are late; never blocks |
| Beleg | receipt | Uploaded file in the user's receipt cache; code `UserReceipt` |
| Meine Belege | my receipts | `/Forms/Receipts` |
| Hinweis | note | Calculator remark with severity Info, Warning or Problem; code `TravelExpenseNoteDTO` |
