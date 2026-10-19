using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Receipts;
using Quartermaster.Data.Receipts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Receipts;

public class ReceiptListEndpoint : EndpointWithoutRequest<UserReceiptListDTO> {
    private readonly UserReceiptRepository _receipts;
    private readonly ReceiptLimits _limits;
    private readonly PermissionContext _perms;

    public ReceiptListEndpoint(UserReceiptRepository receipts, ReceiptLimits limits, PermissionContext perms) {
        _receipts = receipts;
        _limits = limits;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/receipts");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(new UserReceiptListDTO {
            Items = _receipts.ListForOwner(_perms.UserId.Value).Select(r => r.ToDto()).ToList(),
            MaxCount = _limits.MaxCount(),
            MaxFileMegabytes = _limits.MaxFileMegabytes()
        }, cancellation: ct);
    }
}
