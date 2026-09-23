using System.Collections.Generic;
using System.Threading.Tasks;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Data.Forms.PdfTemplates;
using Quartermaster.Server.Forms.PdfTemplating;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

/// <summary>
/// Renders the Auslagenerstattung PDF from the editable <c>PdfTemplate</c> body for the
/// <c>expense-reimbursement</c> key, falling back to the shipped default if the row is missing.
/// The template model exposes <c>form</c> (the input DTO, properties 1:1 with C#) and <c>globals</c>.
/// BIC resolution happens inside the template via the <c>bic_lookup</c> filter.
/// </summary>
public class ExpenseReimbursementPdfService {
    public const string FormKey = "expense-reimbursement";

    private readonly PdfTemplateRepository _templates;
    private readonly PdfTemplateEngine _engine;
    private readonly FormAssets _assets;
    private readonly NotificationTemplateGlobals _globals;

    public ExpenseReimbursementPdfService(PdfTemplateRepository templates, PdfTemplateEngine engine,
        FormAssets assets, NotificationTemplateGlobals globals) {
        _templates = templates;
        _engine = engine;
        _assets = assets;
        _globals = globals;
    }

    public async Task<byte[]> RenderAsync(ExpenseReimbursementInputDTO input) {
        var template = _templates.GetByKey(FormKey);
        var body = template?.Body ?? PdfTemplateDefaults.ByKey[FormKey];
        var model = new Dictionary<string, object> {
            ["form"] = input,
            ["globals"] = _globals.Build()
        };
        return await _engine.RenderAsync(body, model, _assets.Resolve);
    }
}
