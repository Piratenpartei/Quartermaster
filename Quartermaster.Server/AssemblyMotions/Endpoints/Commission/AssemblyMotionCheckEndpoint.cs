using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionCheckEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionCheckRequest> {
    private readonly AssemblyMotionCommissionActions _actions;
    private readonly AssemblyMotionCommissionService _service;

    public AssemblyMotionCheckEndpoint(AssemblyMotionCommissionActions actions, AssemblyMotionCommissionService service) {
        _actions = actions;
        _service = service;
    }

    public override void Configure() {
        Post("/api/assemblymotions/motions/{Id}/check");
    }

    public override async Task HandleAsync(AssemblyMotionCheckRequest req, CancellationToken ct) {
        var result = _actions.Run(req.Id, (userId, nowUtc)
            => _service.Check(req.Id, req.Status, req.CheckNote, userId, nowUtc));
        await SendResultAsync(result, ct);
    }
}
