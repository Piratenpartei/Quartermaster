using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

public class ExpenseReimbursementSubmitEndpoint : Endpoint<ExpenseReimbursementIdRequest> {
    private readonly ExpenseReimbursementRepository _repo;
    private readonly ExpenseReimbursementPdfService _pdfService;
    private readonly AuditLogRepository _audit;
    private readonly PermissionContext _perms;

    public ExpenseReimbursementSubmitEndpoint(ExpenseReimbursementRepository repo,
        ExpenseReimbursementPdfService pdfService, AuditLogRepository audit, PermissionContext perms) {
        _repo = repo;
        _pdfService = pdfService;
        _audit = audit;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/forms/expense-reimbursement/{Id}/submit");
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
        if (entity.Status != ExpenseReimbursementStatus.Draft) {
            AddError("form is already submitted");
            await SendErrorsAsync(409, ct);
            return;
        }

        var now = DateTime.UtcNow;
        _repo.MarkSubmitted(entity.Id, now);
        var items = _repo.GetLineItems(entity.Id);
        var input = entity.ToInputDTO(items);
        var pdf = await _pdfService.RenderAsync(input);

        _audit.Log(nameof(ExpenseReimbursement), entity.Id, "Submit");

        await SendBytesAsync(pdf, fileName: "auslagenerstattung.pdf", contentType: "application/pdf", cancellation: ct);
    }
}
