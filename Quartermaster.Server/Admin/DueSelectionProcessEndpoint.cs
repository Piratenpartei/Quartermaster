using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.DueSelector;

namespace Quartermaster.Server.Admin;

public class DueSelectionProcessRequest {
    public Guid Id { get; set; }
    public int Status { get; set; }
}

public class DueSelectionProcessEndpoint : Endpoint<DueSelectionProcessRequest> {
    private readonly DueSelectionRepository _dueSelectionRepo;

    public DueSelectionProcessEndpoint(DueSelectionRepository dueSelectionRepo) {
        _dueSelectionRepo = dueSelectionRepo;
    }

    public override void Configure() {
        Post("/api/admin/dueselections/process");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(DueSelectionProcessRequest req, CancellationToken ct) {
        var selection = _dueSelectionRepo.Get(req.Id);
        if (selection == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var status = (DueSelectionStatus)req.Status;
        if (status != DueSelectionStatus.Approved && status != DueSelectionStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _dueSelectionRepo.UpdateStatus(req.Id, status, null);
        await SendOkAsync(ct);
    }
}
