using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Receipts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Receipts;

public class ReceiptDeleteRequest {
    public Guid Id { get; set; }
}

public class ReceiptDeleteEndpoint : Endpoint<ReceiptDeleteRequest> {
    private readonly UserReceiptRepository _receipts;
    private readonly ReceiptStorage _storage;
    private readonly PermissionContext _perms;

    public ReceiptDeleteEndpoint(UserReceiptRepository receipts, ReceiptStorage storage, PermissionContext perms) {
        _receipts = receipts;
        _storage = storage;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/receipts/{Id}");
    }

    public override async Task HandleAsync(ReceiptDeleteRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var receipt = _receipts.GetOwned(req.Id, _perms.UserId.Value);
        if (receipt == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        _receipts.Delete(receipt.Id);
        _storage.Delete(receipt.OwnerUserId, receipt.StoredName);
        await SendOkAsync(ct);
    }
}
