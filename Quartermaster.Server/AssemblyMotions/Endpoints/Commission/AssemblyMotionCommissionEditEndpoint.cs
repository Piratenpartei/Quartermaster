using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionCommissionEditEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionCommissionEditRequest> {
    private readonly AssemblyMotionCommissionActions _actions;
    private readonly AssemblyMotionCommissionService _service;

    public AssemblyMotionCommissionEditEndpoint(AssemblyMotionCommissionActions actions, AssemblyMotionCommissionService service) {
        _actions = actions;
        _service = service;
    }

    public override void Configure() {
        Put("/api/assemblymotions/motions/{Id}/commission-edit");
    }

    public override async Task HandleAsync(AssemblyMotionCommissionEditRequest req, CancellationToken ct) {
        var draft = new AssemblyMotionDraft(req.TypeId, req.Title, req.Section, req.TextMarkdown, req.ReasonMarkdown,
            req.Summary, req.Keywords, req.PriorDiscussion);
        var result = _actions.Run(req.Id, (userId, nowUtc)
            => _service.Edit(req.Id, userId, draft, req.BaseRevision, req.ChangeNote, nowUtc));
        await SendResultAsync(result, ct);
    }
}
