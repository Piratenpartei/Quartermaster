using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionCompetitionDeclareEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionCompetitionDeclareRequest> {
    private readonly AssemblyMotionMemberActions _actions;
    private readonly AssemblyMotionCompetitionService _service;

    public AssemblyMotionCompetitionDeclareEndpoint(AssemblyMotionMemberActions actions, AssemblyMotionCompetitionService service) {
        _actions = actions;
        _service = service;
    }

    public override void Configure() {
        Post("/api/assemblymotions/motions/{MotionId}/competitions");
    }

    public override async Task HandleAsync(AssemblyMotionCompetitionDeclareRequest req, CancellationToken ct) {
        var result = _actions.RunAsCommissionOrMember(req.MotionId, true, (_, member, userId, asCommission, nowUtc)
            => _service.Declare(req.MotionId, req.OtherMotionId, userId, member, asCommission, nowUtc));
        await SendResultAsync(result, ct);
    }
}
