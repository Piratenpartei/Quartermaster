using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Forms.ExpenseReimbursements;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

/// <summary>
/// Renders a fresh Auslagenerstattung PDF from the supplied form payload and streams it back to
/// the caller. Anonymous endpoint: no persistence, no audit log, no permission gate. The wizard
/// posts the full form state here once the user clicks "PDF erstellen".
/// </summary>
public class ExpenseReimbursementRenderEndpoint : Endpoint<ExpenseReimbursementInputDTO> {
    private readonly ExpenseReimbursementPdfService _pdfService;

    public ExpenseReimbursementRenderEndpoint(ExpenseReimbursementPdfService pdfService) {
        _pdfService = pdfService;
    }

    public override void Configure() {
        Post("/api/forms/expense-reimbursement/render");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExpenseReimbursementInputDTO req, CancellationToken ct) {
        var pdf = await _pdfService.RenderAsync(req);
        await SendBytesAsync(pdf, fileName: "auslagenerstattung.pdf", contentType: "application/pdf", cancellation: ct);
    }
}
