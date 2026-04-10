using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class RevokeChapterPermissionRequest {
    public Guid UserId { get; set; }
    public Guid ChapterId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}

public class RevokeChapterPermissionEndpoint : Endpoint<RevokeChapterPermissionRequest> {
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly PermissionRepository _permissionRepo;

    public RevokeChapterPermissionEndpoint(UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo, PermissionRepository permissionRepo) {
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _permissionRepo = permissionRepo;
    }

    public override void Configure() {
        Delete("/api/users/{UserId}/permissions/chapter");
    }

    public override async Task HandleAsync(RevokeChapterPermissionRequest req, CancellationToken ct) {
        var callerId = EndpointAuthorizationHelper.GetUserId(User);
        if (callerId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(callerId.Value, PermissionIdentifier.CreateUser, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var permission = _permissionRepo.GetByIdentifier(req.PermissionIdentifier);
        if (permission == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        _chapterPermRepo.RemoveForUser(req.UserId, req.ChapterId, permission.Id);
        await SendOkAsync(ct);
    }
}
