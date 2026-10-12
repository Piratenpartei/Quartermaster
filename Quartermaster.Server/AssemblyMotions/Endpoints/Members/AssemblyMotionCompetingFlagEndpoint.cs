using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionCompetingFlagEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionCompetingFlagRequest> {
    private readonly AssemblyMotionMemberActions _actions;
    private readonly AssemblyMotionCompetitionService _service;

    public AssemblyMotionCompetingFlagEndpoint(AssemblyMotionMemberActions actions, AssemblyMotionCompetitionService service) {
        _actions = actions;
        _service = service;
    }

    public override void Configure() {
        Put("/api/assemblymotions/motions/{Id}/competing-flag");
    }

    public override async Task HandleAsync(AssemblyMotionCompetingFlagRequest req, CancellationToken ct) {
        var result = _actions.RunAsCommissionOrMember(req.Id, req.Value, (_, member, _, asCommission, nowUtc)
            => _service.SetCompetingFlag(req.Id, req.Value, member, asCommission, nowUtc));
        await SendResultAsync(result, ct);
    }
}
