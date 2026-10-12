using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionPortalUpdateEndpoint : Endpoint<AssemblyMotionPortalUpdateRequest, AssemblyMotionPortalSettingsDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionPortalSettingsBuilder _builder;

    public AssemblyMotionPortalUpdateEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionPortalSettingsBuilder builder) {
        _portals = portals;
        _events = events;
        _access = access;
        _builder = builder;
    }

    public override void Configure() {
        Put("/api/assemblymotions/portals/{Id}");
    }

    public override async Task HandleAsync(AssemblyMotionPortalUpdateRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.Id);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanEditPortal(ev)) {
            await SendForbiddenAsync(ct);
            return;
        }

        portal.OpensAt = req.OpensAt.UtcDateTime;
        portal.ClosesAt = req.ClosesAt.UtcDateTime;
        portal.IntroMarkdown = req.IntroMarkdown;
        portal.IntroHtml = MarkdownService.ToHtml(req.IntroMarkdown);
        portal.PrioritizationEnabled = req.PrioritizationEnabled;
        portal.PrioritizationStartsAt = req.PrioritizationStartsAt?.UtcDateTime;
        portal.PrioritizationEndsAt = req.PrioritizationEndsAt?.UtcDateTime;
        portal.WikiMirrorEnabled = req.WikiMirrorEnabled;
        portal.WikiPagePrefix = req.WikiPagePrefix;
        portal.WikiAssemblyLabel = req.WikiAssemblyLabel;
        portal.OpenSlidesMeetingId = req.OpenSlidesMeetingId;
        _portals.Update(portal);

        await SendAsync(_builder.Build(portal, ev), cancellation: ct);
    }
}
