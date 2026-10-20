using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forwarding.Endpoints;

public class ForwardingCaseResolveEndpoint : Endpoint<ForwardingCaseRequest> {
    private readonly ForwardingCaseRepository _cases;
    private readonly ForwardingCaseActions _actions;
    private readonly AuditLogRepository _auditLog;
    private readonly PermissionContext _perms;

    public ForwardingCaseResolveEndpoint(ForwardingCaseRepository cases, ForwardingCaseActions actions, AuditLogRepository auditLog, PermissionContext perms) {
        _cases = cases;
        _actions = actions;
        _auditLog = auditLog;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/forwarding/cases/{Id}/resolve");
    }

    public override async Task HandleAsync(ForwardingCaseRequest req, CancellationToken ct) {
        var forwardingCase = _cases.Get(req.Id);
        if (forwardingCase == null) {
            AddError(I18nKey.Error.Forwarding.CaseNotFound);
            await SendErrorsAsync(404, ct);
            return;
        }
        if (!ForwardingCaseAccess.VisibleKinds(_perms).Contains(forwardingCase.Kind)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (!_actions.Resolve(req.Id, DateTime.UtcNow)) {
            AddError(I18nKey.Error.Forwarding.CaseActionNotPossible);
            await SendErrorsAsync(409, ct);
            return;
        }

        _auditLog.Log("ForwardingCase", req.Id, "Resolve");
        await SendOkAsync(ct);
    }
}
