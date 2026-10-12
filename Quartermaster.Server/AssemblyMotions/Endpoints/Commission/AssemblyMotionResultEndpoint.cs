using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionResultEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionResultRequest> {
    private readonly AssemblyMotionCommissionActions _actions;
    private readonly AssemblyMotionResultService _service;

    public AssemblyMotionResultEndpoint(AssemblyMotionCommissionActions actions, AssemblyMotionResultService service) {
        _actions = actions;
        _service = service;
    }

    public override void Configure() {
        Put("/api/assemblymotions/motions/{Id}/result");
    }

    public override async Task HandleAsync(AssemblyMotionResultRequest req, CancellationToken ct) {
        var result = _actions.Run(req.Id, (_, nowUtc)
            => _service.SetResult(req.Id, req.Result, nowUtc));
        await SendResultAsync(result, ct);
    }
}
