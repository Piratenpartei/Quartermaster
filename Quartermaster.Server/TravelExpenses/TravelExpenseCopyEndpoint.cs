using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>A new draft from an existing application, for the next trip along the same route.</summary>
public class TravelExpenseCopyEndpoint : Endpoint<TravelExpenseIdRequest, TravelExpenseResponseDTO> {
    private readonly TravelExpenseReimbursementRepository _repo;
    private readonly AuditLogRepository _audit;
    private readonly PermissionContext _perms;

    public TravelExpenseCopyEndpoint(TravelExpenseReimbursementRepository repo, AuditLogRepository audit, PermissionContext perms) {
        _repo = repo;
        _audit = audit;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/forms/travel-expense/{Id}/copy");
    }

    public override async Task HandleAsync(TravelExpenseIdRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var source = _repo.Get(req.Id);
        if (source == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (source.OwnerUserId != _perms.UserId.Value) {
            await SendForbiddenAsync(ct);
            return;
        }

        var now = DateTime.UtcNow;
        var input = source.ReadInput();
        input.FormDate = DateTime.Today;
        input.ResumeStep = 0;
        foreach (var module in input.Modules)
            module.Id = Guid.NewGuid();

        var copy = new TravelExpenseReimbursement { OwnerUserId = source.OwnerUserId, CreatedAt = now, UpdatedAt = now };
        copy.WriteInput(input, source.Total);
        _repo.Create(copy);
        _audit.LogCreated(nameof(TravelExpenseReimbursement), copy.Id);

        await SendAsync(copy.ToResponse(), cancellation: ct);
    }
}
