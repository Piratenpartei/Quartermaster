using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionWikiResyncEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionIdRequest> {
    private readonly AssemblyMotionCommissionActions _actions;
    private readonly AssemblyMotionWikiResync _resync;

    public AssemblyMotionWikiResyncEndpoint(AssemblyMotionCommissionActions actions, AssemblyMotionWikiResync resync) {
        _actions = actions;
        _resync = resync;
    }

    public override void Configure() {
        Post("/api/assemblymotions/motions/{Id}/wiki/resync");
    }

    public override async Task HandleAsync(AssemblyMotionIdRequest req, CancellationToken ct) {
        var result = _actions.Run(req.Id, (userId, nowUtc)
            => _resync.QueueOne(req.Id, nowUtc));
        await SendResultAsync(result, ct);
    }
}
