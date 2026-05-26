using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class GetUserPermissionsRequest {
    public Guid UserId { get; set; }
}

public class GetUserPermissionsEndpoint : Endpoint<GetUserPermissionsRequest, UserPermissionsDTO> {
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly PermissionContext _perms;

    public GetUserPermissionsEndpoint(UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo, PermissionContext perms) {
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/{UserId}/permissions");
    }

    public override async Task HandleAsync(GetUserPermissionsRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewUsers)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var globalPerms = _globalPermRepo.GetForUser(req.UserId);
        var chapterPerms = _chapterPermRepo.GetAllForUser(req.UserId);

        var dto = new UserPermissionsDTO {
            GlobalPermissions = globalPerms.Select(p => p.Identifier).ToList(),
            ChapterPermissions = chapterPerms.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value)
        };

        await SendAsync(dto, cancellation: ct);
    }
}
