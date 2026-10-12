using System;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public abstract class AssemblyMotionActionEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionIdRequest> {
    private readonly AssemblyMotionMemberActions _actions;

    protected AssemblyMotionActionEndpoint(AssemblyMotionMemberActions actions) {
        _actions = actions;
    }

    protected abstract bool RequiresEligibility { get; }

    protected abstract AssemblyMotionOutcome Execute(Guid motionId, Member member, DateTime nowUtc);

    public override async Task HandleAsync(AssemblyMotionIdRequest req, CancellationToken ct) {
        var result = _actions.Run(req.Id, RequiresEligibility, (_, member, _, nowUtc) => Execute(req.Id, member, nowUtc));
        await SendResultAsync(result, ct);
    }
}
