using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

public class ExpenseReimbursementListMineEndpoint : EndpointWithoutRequest<List<ExpenseReimbursementListItemDTO>> {
    private readonly ExpenseReimbursementRepository _repo;
    private readonly PermissionContext _perms;

    public ExpenseReimbursementListMineEndpoint(ExpenseReimbursementRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forms/expense-reimbursement");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var forms = _repo.GetForOwner(_perms.UserId.Value);
        var totals = _repo.GetLineItemTotals(forms.Select(f => f.Id).ToList());

        var items = forms
            .Select(f => new ExpenseReimbursementListItemDTO {
                Id = f.Id,
                Status = (ExpenseReimbursementStatusDTO)(int)f.Status,
                ChapterName = f.ChapterNameSnapshot,
                Total = totals.TryGetValue(f.Id, out var t) ? t : 0m,
                UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(f.UpdatedAt, DateTimeKind.Utc)),
                SubmittedAt = f.SubmittedAt.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(f.SubmittedAt.Value, DateTimeKind.Utc))
                    : null
            })
            .ToList();

        await SendAsync(items, cancellation: ct);
    }
}
