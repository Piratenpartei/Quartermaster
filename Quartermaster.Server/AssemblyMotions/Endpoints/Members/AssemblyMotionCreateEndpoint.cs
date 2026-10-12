using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionCreateEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionSaveRequest> {
    private readonly AssemblyMotionMemberActions _actions;
    private readonly AssemblyMotionService _service;

    public AssemblyMotionCreateEndpoint(AssemblyMotionMemberActions actions, AssemblyMotionService service) {
        _actions = actions;
        _service = service;
    }

    public override void Configure() {
        Post("/api/assemblymotions/portals/{PortalId}/motions");
    }

    public override async Task HandleAsync(AssemblyMotionSaveRequest req, CancellationToken ct) {
        var draft = new AssemblyMotionDraft(req.TypeId, req.Title.Trim(), req.Section, req.TextMarkdown, req.ReasonMarkdown,
            req.Summary, req.Keywords, req.PriorDiscussion);
        var result = _actions.Create(req.PortalId, (portal, member, userId, nowUtc)
            => _service.CreateDraft(portal, member, userId, draft, nowUtc));
        await SendResultAsync(result, ct);
    }
}
