using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionUpdateEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionSaveRequest> {
    private readonly AssemblyMotionMemberActions _actions;
    private readonly AssemblyMotionService _service;

    public AssemblyMotionUpdateEndpoint(AssemblyMotionMemberActions actions, AssemblyMotionService service) {
        _actions = actions;
        _service = service;
    }

    public override void Configure() {
        Put("/api/assemblymotions/motions/{Id}");
    }

    public override async Task HandleAsync(AssemblyMotionSaveRequest req, CancellationToken ct) {
        var draft = new AssemblyMotionDraft(req.TypeId, req.Title.Trim(), req.Section, req.TextMarkdown, req.ReasonMarkdown,
            req.Summary, req.Keywords, req.PriorDiscussion);
        var result = _actions.Run(req.Id, true, (motion, member, userId, nowUtc)
            => _service.UpdateByAuthor(motion, member, userId, draft, req.BaseRevision, nowUtc));
        await SendResultAsync(result, ct);
    }
}
