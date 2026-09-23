using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

/// <summary>Re-renders the PDF for an already-submitted (or draft) form so the owner can download it again without re-submitting.</summary>
public class ExpenseReimbursementPdfEndpoint : Endpoint<ExpenseReimbursementIdRequest> {
    private readonly ExpenseReimbursementRepository _repo;
    private readonly ExpenseReimbursementPdfService _pdfService;
    private readonly PermissionContext _perms;

    public ExpenseReimbursementPdfEndpoint(ExpenseReimbursementRepository repo,
        ExpenseReimbursementPdfService pdfService, PermissionContext perms) {
        _repo = repo;
        _pdfService = pdfService;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forms/expense-reimbursement/{Id}/pdf");
    }

    public override async Task HandleAsync(ExpenseReimbursementIdRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var entity = _repo.Get(req.Id);
        if (entity == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (entity.OwnerUserId != _perms.UserId.Value) {
            await SendForbiddenAsync(ct);
            return;
        }
        var items = _repo.GetLineItems(entity.Id);
        var input = entity.ToInputDTO(items);
        var pdf = await _pdfService.RenderAsync(input);
        await SendBytesAsync(pdf, fileName: "auslagenerstattung.pdf", contentType: "application/pdf", cancellation: ct);
    }
}
