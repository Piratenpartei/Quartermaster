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

public class GrantChapterPermissionRequest {
    public Guid UserId { get; set; }
    public Guid ChapterId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}

public class GrantChapterPermissionEndpoint : Endpoint<GrantChapterPermissionRequest> {
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly PermissionRepository _permissionRepo;

    public GrantChapterPermissionEndpoint(UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo, PermissionRepository permissionRepo) {
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _permissionRepo = permissionRepo;
    }

    public override void Configure() {
        Post("/api/users/{UserId}/permissions/chapter");
    }

    public override async Task HandleAsync(GrantChapterPermissionRequest req, CancellationToken ct) {
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

        _chapterPermRepo.AddForUser(req.UserId, req.ChapterId, permission.Id);
        await SendOkAsync(ct);
    }
}
