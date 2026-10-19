using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Receipts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Receipts;

public class ReceiptFileRequest {
    public Guid Id { get; set; }
}

public class ReceiptFileEndpoint : Endpoint<ReceiptFileRequest> {
    private readonly UserReceiptRepository _receipts;
    private readonly ReceiptStorage _storage;
    private readonly PermissionContext _perms;

    public ReceiptFileEndpoint(UserReceiptRepository receipts, ReceiptStorage storage, PermissionContext perms) {
        _receipts = receipts;
        _storage = storage;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/receipts/{Id}/file");
    }

    public override async Task HandleAsync(ReceiptFileRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var receipt = _receipts.GetOwned(req.Id, _perms.UserId.Value);
        if (receipt == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var path = _storage.PathOf(receipt.OwnerUserId, receipt.StoredName);
        if (!File.Exists(path)) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendStreamAsync(File.OpenRead(path), receipt.FileName, receipt.SizeBytes, receipt.ContentType, cancellation: ct);
    }
}
