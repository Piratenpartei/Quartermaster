using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

/// <summary>Works for deleted motions and invisible events too, so a stale subscription can always be removed.</summary>
public class AssemblyMotionUnsubscribeEndpoint : Endpoint<AssemblyMotionIdRequest> {
    private readonly AssemblyMotionSubscriptionRepository _subscriptions;
    private readonly DbContext _db;
    private readonly PermissionContext _perms;

    public AssemblyMotionUnsubscribeEndpoint(AssemblyMotionSubscriptionRepository subscriptions, DbContext db, PermissionContext perms) {
        _subscriptions = subscriptions;
        _db = db;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/assemblymotions/motions/{Id}/subscription");
    }

    public override async Task HandleAsync(AssemblyMotionIdRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_db.AssemblyMotions.Any(m => m.Id == req.Id)) {
            await SendNotFoundAsync(ct);
            return;
        }

        _subscriptions.Unsubscribe(req.Id, _perms.UserId.Value);
        await SendNoContentAsync(ct);
    }
}
