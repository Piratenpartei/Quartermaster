using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionCompetitionRemoveEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionCompetitionIdRequest> {
    private readonly AssemblyMotionMemberActions _actions;
    private readonly AssemblyMotionCompetitionRepository _competitions;
    private readonly AssemblyMotionCompetitionService _service;

    public AssemblyMotionCompetitionRemoveEndpoint(AssemblyMotionMemberActions actions,
        AssemblyMotionCompetitionRepository competitions, AssemblyMotionCompetitionService service) {
        _actions = actions;
        _competitions = competitions;
        _service = service;
    }

    public override void Configure() {
        Delete("/api/assemblymotions/competitions/{Id}");
    }

    public override async Task HandleAsync(AssemblyMotionCompetitionIdRequest req, CancellationToken ct) {
        var link = _competitions.Get(req.Id);
        if (link == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var result = _actions.RunAsCommissionOrMember(link.DeclaringMotionId, false, (_, member, userId, asCommission, nowUtc)
            => _service.Remove(req.Id, userId, member, asCommission, nowUtc));
        await SendResultAsync(result, ct);
    }
}
