using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Server.Banking;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

/// <summary>
/// Renders a fresh Auslagenerstattung PDF from the supplied form payload and streams it back to
/// the caller. Anonymous endpoint: no persistence, no audit log, no permission gate. The wizard
/// posts the full form state here once the user clicks "PDF erstellen".
/// </summary>
public class ExpenseReimbursementRenderEndpoint : Endpoint<ExpenseReimbursementInputDTO> {
    private readonly BankCodeBicLookup _bicLookup;
    private readonly FormAssets _assets;

    public ExpenseReimbursementRenderEndpoint(BankCodeBicLookup bicLookup, FormAssets assets) {
        _bicLookup = bicLookup;
        _assets = assets;
    }

    public override void Configure() {
        Post("/api/forms/expense-reimbursement/render");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExpenseReimbursementInputDTO req, CancellationToken ct) {
        var resolvedBic = _bicLookup.LookupByIban(req.Iban);
        var pdf = ExpenseReimbursementPdfRenderer.Render(req, resolvedBic, _assets.PirateLogoSvg);
        await SendBytesAsync(pdf, fileName: "auslagenerstattung.pdf", contentType: "application/pdf", cancellation: ct);
    }
}
