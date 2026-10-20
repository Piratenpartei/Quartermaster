using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Public;

public class AssemblyMotionPublicDetailEndpoint : Endpoint<AssemblyMotionPublicDetailRequest, AssemblyMotionPublicDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionUpvoteRepository _upvotes;
    private readonly AssemblyMotionCompetitionViewBuilder _competitionViews;
    private readonly AssemblyMotionPublicAccess _access;

    public AssemblyMotionPublicDetailEndpoint(
        AssemblyMotionPortalRepository portals,
        AssemblyMotionRepository motions,
        AssemblyMotionUpvoteRepository upvotes,
        AssemblyMotionCompetitionViewBuilder competitionViews,
        AssemblyMotionPublicAccess access) {
        _portals = portals;
        _motions = motions;
        _upvotes = upvotes;
        _competitionViews = competitionViews;
        _access = access;
    }

    public override void Configure() {
        Get("/api/assemblymotions/motions/{Id}/public");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AssemblyMotionPublicDetailRequest req, CancellationToken ct) {
        var motion = _motions.Get(req.Id);
        var type = motion == null ? null : _portals.GetType(motion.TypeId);
        var portal = motion == null ? null : _access.GetPortal(motion);
        if (motion == null || type == null || portal == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var dto = AssemblyMotionPublicView.Build(motion, type);
        dto.PortalId = portal.Id;
        dto.EventId = portal.EventId;
        dto.Competitions = _competitionViews.BuildPublic(motion);
        if (AssemblyMotionUpvoteService.IsCountPublic(portal, motion, DateTime.UtcNow))
            dto.UpvoteCount = _upvotes.CountForMotion(motion.Id);
        await SendAsync(dto, cancellation: ct);
    }
}
