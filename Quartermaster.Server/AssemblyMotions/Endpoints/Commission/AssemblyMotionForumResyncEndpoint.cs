using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;
using Quartermaster.Server.AssemblyMotions.Forum;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionForumResyncEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionIdRequest> {
    private readonly AssemblyMotionCommissionActions _actions;
    private readonly AssemblyMotionForumResync _resync;

    public AssemblyMotionForumResyncEndpoint(AssemblyMotionCommissionActions actions, AssemblyMotionForumResync resync) {
        _actions = actions;
        _resync = resync;
    }

    public override void Configure() {
        Post("/api/assemblymotions/motions/{Id}/forum/resync");
    }

    public override async Task HandleAsync(AssemblyMotionIdRequest req, CancellationToken ct) {
        var result = _actions.Run(req.Id, (userId, nowUtc)
            => _resync.QueueOne(req.Id, nowUtc));
        await SendResultAsync(result, ct);
    }
}
