using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class RevokeChapterPermissionEndpoint : Endpoint<RevokeChapterPermissionRequest> {
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly PermissionRepository _permissionRepo;
    private readonly PermissionContext _perms;

    public RevokeChapterPermissionEndpoint(UserChapterPermissionRepository chapterPermRepo,
        PermissionRepository permissionRepo, PermissionContext perms) {
        _chapterPermRepo = chapterPermRepo;
        _permissionRepo = permissionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/users/{UserId}/permissions/chapter");
    }

    public override async Task HandleAsync(RevokeChapterPermissionRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.CreateUser)) {
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
