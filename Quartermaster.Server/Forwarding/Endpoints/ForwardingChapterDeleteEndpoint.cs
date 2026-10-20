using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forwarding.Endpoints;

public class ForwardingChapterDeleteEndpoint : Endpoint<ForwardingChapterRequest> {
    private readonly ForwardingSettingsRepository _settings;
    private readonly AuditLogRepository _auditLog;
    private readonly PermissionContext _perms;

    public ForwardingChapterDeleteEndpoint(ForwardingSettingsRepository settings, AuditLogRepository auditLog, PermissionContext perms) {
        _settings = settings;
        _auditLog = auditLog;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/forwarding/chapters/{ChapterId}");
    }

    public override async Task HandleAsync(ForwardingChapterRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (!_settings.DeleteChapter(req.ChapterId)) {
            await SendNotFoundAsync(ct);
            return;
        }

        _auditLog.Log("ForwardingChapterSetting", req.ChapterId, "Deleted");
        await SendOkAsync(ct);
    }
}
