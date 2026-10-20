using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Forwarding.Redmine;

namespace Quartermaster.Server.Forwarding.Endpoints;

/// <summary>Checks the chapter's own stored connection with one read-only request. Only fixed texts reach the caller.</summary>
public class RedmineConnectionCheckEndpoint : Endpoint<ForwardingChapterRequest, ConnectionCheckResultDTO> {
    private readonly RedmineConnectionRepository _connections;
    private readonly IRedmineClient _redmine;
    private readonly PermissionContext _perms;
    private readonly ILogger<RedmineConnectionCheckEndpoint> _logger;

    public RedmineConnectionCheckEndpoint(RedmineConnectionRepository connections, IRedmineClient redmine, PermissionContext perms, ILogger<RedmineConnectionCheckEndpoint> logger) {
        _connections = connections;
        _redmine = redmine;
        _perms = perms;
        _logger = logger;
    }

    public override void Configure() {
        Post("/api/forwarding/redmine-connections/{ChapterId}/check");
    }

    public override async Task HandleAsync(ForwardingChapterRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var connection = _connections.Get(req.ChapterId);
        if (connection == null) {
            AddError(I18nKey.Error.Forwarding.ConnectionNotFound);
            await SendErrorsAsync(404, ct);
            return;
        }

        try {
            var name = await _redmine.GetCurrentUserNameAsync(new RedmineConnectionSettings(connection.BaseUrl, connection.ApiKey), ct);
            await SendAsync(new ConnectionCheckResultDTO { Ok = true, Message = $"Verbindung erfolgreich: angemeldet als {name}." }, cancellation: ct);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Redmine connection check failed: {Exception}", ex);
            var message = ex is RedmineException ? ex.Message : "Unerwarteter Fehler, Details stehen im Serverprotokoll.";
            await SendAsync(new ConnectionCheckResultDTO { Ok = false, Message = message }, cancellation: ct);
        }
    }
}
