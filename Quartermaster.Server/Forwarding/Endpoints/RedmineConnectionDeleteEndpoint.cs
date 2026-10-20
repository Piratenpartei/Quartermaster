using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forwarding.Endpoints;

public class RedmineConnectionDeleteEndpoint : Endpoint<ForwardingChapterRequest> {
    private readonly RedmineConnectionRepository _connections;
    private readonly AuditLogRepository _auditLog;
    private readonly PermissionContext _perms;

    public RedmineConnectionDeleteEndpoint(RedmineConnectionRepository connections, AuditLogRepository auditLog, PermissionContext perms) {
        _connections = connections;
        _auditLog = auditLog;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/forwarding/redmine-connections/{ChapterId}");
    }

    public override async Task HandleAsync(ForwardingChapterRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (!_connections.Delete(req.ChapterId)) {
            await SendNotFoundAsync(ct);
            return;
        }

        _auditLog.Log("RedmineConnection", req.ChapterId, "Deleted");
        await SendOkAsync(ct);
    }
}
