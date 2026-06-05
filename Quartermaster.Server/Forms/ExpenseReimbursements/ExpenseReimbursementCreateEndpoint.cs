using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

public class ExpenseReimbursementCreateEndpoint : EndpointWithoutRequest<ExpenseReimbursementResponseDTO> {
    private readonly ExpenseReimbursementRepository _repo;
    private readonly PermissionContext _perms;

    public ExpenseReimbursementCreateEndpoint(ExpenseReimbursementRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/forms/expense-reimbursement");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var now = DateTime.UtcNow;
        var entity = new ExpenseReimbursement {
            Id = Guid.NewGuid(),
            OwnerUserId = _perms.UserId.Value,
            Status = ExpenseReimbursementStatus.Draft,
            MotionDate = now.Date,
            FormDate = now.Date,
            CreatedAt = now,
            UpdatedAt = now
        };
        _repo.Create(entity, Array.Empty<ExpenseReimbursementLineItem>());
        await SendAsync(entity.ToResponseDTO(Array.Empty<ExpenseReimbursementLineItem>()), cancellation: ct);
    }
}
