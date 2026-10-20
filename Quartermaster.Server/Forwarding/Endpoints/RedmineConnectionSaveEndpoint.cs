using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Hosting;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Forwarding.Redmine;

namespace Quartermaster.Server.Forwarding.Endpoints;

public class RedmineConnectionSaveEndpoint : Endpoint<RedmineConnectionSaveRequest> {
    private readonly RedmineConnectionRepository _connections;
    private readonly ChapterRepository _chapters;
    private readonly AuditLogRepository _auditLog;
    private readonly PermissionContext _perms;
    private readonly bool _allowLoopbackHttp;

    public RedmineConnectionSaveEndpoint(RedmineConnectionRepository connections, ChapterRepository chapters, AuditLogRepository auditLog, PermissionContext perms,
        IHostEnvironment environment) {
        _allowLoopbackHttp = environment.IsDevelopment();
        _connections = connections;
        _chapters = chapters;
        _auditLog = auditLog;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/forwarding/redmine-connections/{ChapterId}");
    }

    public override async Task HandleAsync(RedmineConnectionSaveRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (_chapters.Get(req.ChapterId) == null) {
            AddError(I18nKey.Error.Forwarding.ChapterNotFound);
            await SendErrorsAsync(404, ct);
            return;
        }

        var baseUrl = req.BaseUrl.Trim().TrimEnd('/');
        var apiKey = string.IsNullOrWhiteSpace(req.ApiKey) ? null : req.ApiKey.Trim();
        if (!IsValidUrl(baseUrl)) {
            AddError(I18nKey.Error.Forwarding.BaseUrlInvalid);
            await SendErrorsAsync(400, ct);
            return;
        }
        // A stored key is only kept for the same origin; otherwise anyone allowed to edit options could send it to a host of their choice.
        var existing = _connections.Get(req.ChapterId);
        if (apiKey == null && (existing == null || !SameOrigin(existing.BaseUrl, baseUrl))) {
            AddError(I18nKey.Error.Forwarding.ApiKeyRequired);
            await SendErrorsAsync(400, ct);
            return;
        }

        _connections.Upsert(req.ChapterId, baseUrl, apiKey, DateTime.UtcNow);
        _auditLog.Log("RedmineConnection", req.ChapterId, "Saved", "BaseUrl", null, baseUrl);
        await SendOkAsync(ct);
    }

    private static bool SameOrigin(string storedUrl, string newUrl) {
        if (!Uri.TryCreate(storedUrl, UriKind.Absolute, out var stored) || !Uri.TryCreate(newUrl, UriKind.Absolute, out var updated))
            return false;
        return Uri.Compare(stored, updated, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private bool IsValidUrl(string url) {
        if (url.Length == 0 || url.Length > RedmineConnection.BaseUrlMaxLength)
            return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            return false;
        return new RedmineConnectionSettings(url, "").IsSecureUrl(_allowLoopbackHttp);
    }
}
