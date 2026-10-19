using System.Collections.Generic;
using Quartermaster.Server.Forms.ExpenseReimbursements;
using Quartermaster.Server.TravelExpenses;

namespace Quartermaster.Server.Forms.PdfTemplating;

/// <summary>
/// Maps each form key to the template body that ships with the build. Used to seed the
/// <c>PdfTemplates</c> table on startup (only when a row for the key is missing, so admin
/// overrides survive) and to reset a template to its shipped default.
/// </summary>
public static class PdfTemplateDefaults {
    public static readonly IReadOnlyDictionary<string, string> ByKey = new Dictionary<string, string> {
        ["expense-reimbursement"] = ExpenseReimbursementDefaultTemplate.Body,
        ["travel-expense"] = TravelExpenseDefaultTemplate.Body
    };
}
