using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Hosting;
using Quartermaster.Api;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.NewsPosts.Endpoints;

/// <summary>Runs the full programme sync inline; a first sync embeds several hundred sections and can take up to a minute, so callers show a spinner.</summary>
public class ProgrammeSyncEndpoint : EndpointWithoutRequest {
    private readonly ProgrammeSyncService _syncService;
    private readonly NewsPostsSettings _settings;
    private readonly PermissionContext _perms;
    private readonly IHostApplicationLifetime _lifetime;

    public ProgrammeSyncEndpoint(ProgrammeSyncService syncService, NewsPostsSettings settings, PermissionContext perms, IHostApplicationLifetime lifetime) {
        _syncService = syncService;
        _settings = settings;
        _perms = perms;
        _lifetime = lifetime;
    }

    public override void Configure() {
        Post("/api/newsposts/sources/sync");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (!_settings.HasMistral) {
            AddError("Mistral ist nicht konfiguriert.");
            await SendErrorsAsync(409, ct);
            return;
        }

        // Runs on the application-stopping token, not the request-aborted one: a client
        // navigating away or the request timing out must not abort a sync that is already
        // writing sections, since a partial write would leave the page's rows inconsistent.
        if (!await _syncService.TrySyncAllAsync(_lifetime.ApplicationStopping)) {
            AddError("Die Synchronisierung läuft bereits.");
            await SendErrorsAsync(409, ct);
            return;
        }
        await SendNoContentAsync(ct);
    }
}
