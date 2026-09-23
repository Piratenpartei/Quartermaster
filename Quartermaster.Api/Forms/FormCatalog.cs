using System.Collections.Generic;
using Quartermaster.Api.I18n;

namespace Quartermaster.Api.Forms;

/// <summary>One PDF form the wizard engine knows how to fill, surfaced on the public <c>/Formulare</c> catalog page.</summary>
public record FormCatalogEntry(
    string FormKey,
    string DisplayNameI18nKey,
    string DescriptionI18nKey,
    string Icon,
    string Route,
    /// <summary>Comma-separated DTO type names this form's PDF template can bind to, for the editor's field palette (e.g. <c>ExpenseReimbursementInputDTO</c>). <c>globals</c> is always added.</summary>
    string PaletteModels
);

/// <summary>
/// Static registry of all forms available to the user. Both the server (to gate / list) and the
/// client (to render the catalog page) consume this list. Adding a new form means appending an
/// entry here plus its routes/endpoints.
/// </summary>
public static class FormCatalog {
    public static readonly IReadOnlyList<FormCatalogEntry> Entries = new[] {
        new FormCatalogEntry(
            FormKey: "expense-reimbursement",
            DisplayNameI18nKey: I18nKey.Ui.ExpenseReimbursement.PageTitle,
            DescriptionI18nKey: I18nKey.Ui.FormCatalog.ExpenseReimbursementDescription,
            Icon: "bi-receipt",
            Route: "/Formulare/auslagenerstattung",
            PaletteModels: "ExpenseReimbursementInputDTO"
        )
    };
}
