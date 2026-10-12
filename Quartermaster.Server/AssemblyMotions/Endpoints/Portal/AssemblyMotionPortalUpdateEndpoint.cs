using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Rendering;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionPortalUpdateEndpoint : Endpoint<AssemblyMotionPortalUpdateRequest, AssemblyMotionPortalSettingsDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionPortalSettingsBuilder _builder;
    private readonly AssemblyMotionWikiConnectionCheck _wikiCheck;
    private readonly AssemblyMotionWikiResync _wikiResync;

    public AssemblyMotionPortalUpdateEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionPortalSettingsBuilder builder,
        AssemblyMotionWikiConnectionCheck wikiCheck, AssemblyMotionWikiResync wikiResync) {
        _portals = portals;
        _events = events;
        _access = access;
        _builder = builder;
        _wikiCheck = wikiCheck;
        _wikiResync = wikiResync;
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

        var enablesMirror = req.WikiMirrorEnabled && !portal.WikiMirrorEnabled;
        var disablesMirror = !req.WikiMirrorEnabled && portal.WikiMirrorEnabled;
        var changesPrefix = req.WikiMirrorEnabled && portal.WikiMirrorEnabled && portal.WikiPagePrefix != req.WikiPagePrefix;
        if (enablesMirror) {
            // The login runs before anything is written and outside any transaction.
            var check = await _wikiCheck.CheckAsync(ct);
            if (!check.Configured) {
                AddError(I18nKey.Error.AssemblyMotion.WikiNotConfigured);
                await SendErrorsAsync(422, ct);
                return;
            }
            if (!check.Ok) {
                AddError(I18nParams.With(I18nKey.Error.AssemblyMotion.WikiLoginFailed, ("reason", check.Message)));
                await SendErrorsAsync(422, ct);
                return;
            }
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
        if (enablesMirror || changesPrefix)
            _wikiResync.QueueNumbered(portal.Id, DateTime.UtcNow);
        if (disablesMirror)
            _wikiResync.ResetPending(portal.Id);

        await SendAsync(_builder.Build(portal, ev), cancellation: ct);
    }
}
