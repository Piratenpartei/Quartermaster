using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionCommissionWithdrawEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionIdRequest> {
    private readonly AssemblyMotionCommissionActions _actions;
    private readonly AssemblyMotionCommissionService _service;

    public AssemblyMotionCommissionWithdrawEndpoint(AssemblyMotionCommissionActions actions, AssemblyMotionCommissionService service) {
        _actions = actions;
        _service = service;
    }

    public override void Configure() {
        Post("/api/assemblymotions/motions/{Id}/commission-withdraw");
    }

    public override async Task HandleAsync(AssemblyMotionIdRequest req, CancellationToken ct) {
        var result = _actions.Run(req.Id, (userId, nowUtc)
            => _service.Withdraw(req.Id, userId, nowUtc));
        await SendResultAsync(result, ct);
    }
}
