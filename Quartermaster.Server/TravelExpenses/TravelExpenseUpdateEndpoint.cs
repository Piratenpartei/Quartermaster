using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseUpdateRequest : TravelExpenseInputDTO {
    public Guid Id { get; set; }
}

public class TravelExpenseUpdateValidator : Validator<TravelExpenseUpdateRequest> {
    public TravelExpenseUpdateValidator() {
        Include(new TravelExpenseInputRules());
    }
}

public class TravelExpenseUpdateEndpoint : Endpoint<TravelExpenseUpdateRequest, TravelExpenseResponseDTO> {
    private readonly TravelExpenseReimbursementRepository _repo;
    private readonly TravelExpenseService _service;
    private readonly PermissionContext _perms;

    public TravelExpenseUpdateEndpoint(TravelExpenseReimbursementRepository repo, TravelExpenseService service, PermissionContext perms) {
        _repo = repo;
        _service = service;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/forms/travel-expense/{Id}");
    }

    public override async Task HandleAsync(TravelExpenseUpdateRequest req, CancellationToken ct) {
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

        TravelExpenseInputDTO input = req;
        form.WriteInput(input, _service.Calculate(input).Total);
        form.UpdatedAt = DateTime.UtcNow;
        _repo.Update(form);

        await SendAsync(form.ToResponse(), cancellation: ct);
    }
}
