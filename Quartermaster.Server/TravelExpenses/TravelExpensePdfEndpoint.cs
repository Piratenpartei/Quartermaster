using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>The PDF of an own application, drafts included, rendered from the stored input.</summary>
public class TravelExpensePdfEndpoint : Endpoint<TravelExpenseIdRequest> {
    private readonly TravelExpenseReimbursementRepository _repo;
    private readonly TravelExpensePdfService _pdf;
    private readonly PermissionContext _perms;

    public TravelExpensePdfEndpoint(TravelExpenseReimbursementRepository repo, TravelExpensePdfService pdf, PermissionContext perms) {
        _repo = repo;
        _pdf = pdf;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forms/travel-expense/{Id}/pdf");
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

        var pdf = await _pdf.RenderAsync(form.ReadInput(), form.OwnerUserId);
        await SendBytesAsync(pdf, fileName: "reisekosten.pdf", contentType: "application/pdf", cancellation: ct);
    }
}
