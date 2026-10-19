using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Receipts;
using Quartermaster.Data.Receipts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Receipts;

public class ReceiptRenameEndpoint : Endpoint<UserReceiptRenameRequest> {
    private readonly UserReceiptRepository _receipts;
    private readonly PermissionContext _perms;

    public ReceiptRenameEndpoint(UserReceiptRepository receipts, PermissionContext perms) {
        _receipts = receipts;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/receipts/{Id}");
    }

    public override async Task HandleAsync(UserReceiptRenameRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var receipt = _receipts.GetOwned(req.Id, _perms.UserId.Value);
        if (receipt == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var label = req.Label.Trim();
        _receipts.Rename(receipt.Id, label.Length <= UserReceipt.LabelMaxLength ? label : label[..UserReceipt.LabelMaxLength]);
        await SendOkAsync(ct);
    }
}
