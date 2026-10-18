# Public Form Wizards (V5.1)

The membership application and the due selection move from one page per step to one `FormWizard` page each. Dues and payment become shared step components, which removes the `ReturnUrl` passing, the flow detection on shared pages and the summary redirect into the declarations. Submit endpoints and DTOs stay.

## Routes

- `/MembershipApplication` (alias `/MembershipApplication/PersonalData`)
- `/DueSelector` (alias `/DueSelector/UserDataInput`)
- the other step pages and `PublicFormStepper` are deleted; the navbar links point to the new routes

## Steps

Membership application: personal data, address (Germany or abroad as choice cards; for Germany the municipality search, then the address fields; changing the country clears the address), dues, payment, declarations, overview. The overview shows everything, including the full dues and payment summary.

Due selection: personal data (member number; name and email prefilled and locked when logged in), dues, payment, overview.

Shared, in `Pages/DueSelector`:
- `DuesStep`: selectable `CardButton`s (1 %, monthly income group, underage, reduced) with the sub-form of the selected one below; reduced offers "also classify by income" or "skip"
- `PaymentStep`: direct debit, account holder, IBAN, payment schedule
- `DuesSummary`: dues and payment cards of the overview

The wizard's submit button submits; the confirmation notice replaces the wizard afterwards.

## FormWizard

- the current step is mirrored as `?step=N` (1-based) in the URL for every wizard, so browser back and forward move between steps; a deep link or reload lands on the step when all earlier steps are valid
- step bodies are components passed as render fragments from the code-behind

## Validation

- completeness rules live on `MembershipApplicationEntryState` and `DueSelectorEntryState`; the dues amount is computed in `DueSelectorEntryState.RecalculateDue()` instead of during rendering
- a refused Next shows a toast, like the expense form
- direct debit requires an account holder and a valid IBAN: `Quartermaster.Api.Banking.Iban` checks format, the SEPA country length and the ISO 13616 mod-97 checksum
- the server applies the same rule in `DueSelectionDTOValidator` and to the dues part of `MembershipApplicationDTOValidator`; the expense form uses the same IBAN check

## Tests

- unit tests for `Iban`, the completeness rules and `RecalculateDue`
- endpoint tests for the IBAN and account holder rules
- the two Playwright tests get the new routes
