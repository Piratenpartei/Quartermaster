using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>Freezes a draft and returns its PDF with the receipts attached.</summary>
public class TravelExpenseSubmitEndpoint : Endpoint<TravelExpenseIdRequest> {
    private readonly TravelExpenseReimbursementRepository _repo;
    private readonly TravelExpensePdfService _pdf;
    private readonly AuditLogRepository _audit;
    private readonly PermissionContext _perms;

    public TravelExpenseSubmitEndpoint(TravelExpenseReimbursementRepository repo, TravelExpensePdfService pdf, AuditLogRepository audit, PermissionContext perms) {
        _repo = repo;
        _pdf = pdf;
        _audit = audit;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/forms/travel-expense/{Id}/submit");
    }

    public override async Task HandleAsync(TravelExpenseIdRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var form = _repo.Get(req.Id);
        if (form == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (form.OwnerUserId != _perms.UserId.Value) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (form.Status != TravelExpenseStatus.Draft) {
            AddError("form is already submitted");
            await SendErrorsAsync(409, ct);
            return;
        }

        var pdf = await _pdf.RenderAsync(form.ReadInput(), form.OwnerUserId);
        _repo.MarkSubmitted(form.Id, DateTime.UtcNow);
        _audit.Log(nameof(TravelExpenseReimbursement), form.Id, "Submit");

        await SendBytesAsync(pdf, fileName: "reisekosten.pdf", contentType: "application/pdf", cancellation: ct);
    }
}
