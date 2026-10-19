using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseCreateValidator : Validator<TravelExpenseInputDTO> {
    public TravelExpenseCreateValidator() {
        Include(new TravelExpenseInputRules());
    }
}

public class TravelExpenseCreateEndpoint : Endpoint<TravelExpenseInputDTO, TravelExpenseResponseDTO> {
    private readonly TravelExpenseReimbursementRepository _repo;
    private readonly TravelExpenseService _service;
    private readonly AuditLogRepository _audit;
    private readonly PermissionContext _perms;

    public TravelExpenseCreateEndpoint(TravelExpenseReimbursementRepository repo, TravelExpenseService service, AuditLogRepository audit, PermissionContext perms) {
        _repo = repo;
        _service = service;
        _audit = audit;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/forms/travel-expense");
    }

    public override async Task HandleAsync(TravelExpenseInputDTO req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        _service.Prefill(req, _perms.UserId.Value);
        var now = DateTime.UtcNow;
        var form = new TravelExpenseReimbursement { OwnerUserId = _perms.UserId.Value, CreatedAt = now, UpdatedAt = now };
        form.WriteInput(req, _service.Calculate(req).Total);
        _repo.Create(form);
        _audit.LogCreated(nameof(TravelExpenseReimbursement), form.Id);

        await SendAsync(form.ToResponse(), cancellation: ct);
    }
}
