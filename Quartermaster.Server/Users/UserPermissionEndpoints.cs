using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

// GET /api/users/{UserId}/permissions
public class GetUserPermissionsRequest {
    public Guid UserId { get; set; }
}

public class GetUserPermissionsEndpoint : Endpoint<GetUserPermissionsRequest, UserPermissionsDTO> {
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public GetUserPermissionsEndpoint(UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo) {
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Get("/api/users/{UserId}/permissions");
    }

    public override async Task HandleAsync(GetUserPermissionsRequest req, CancellationToken ct) {
        var callerId = EndpointAuthorizationHelper.GetUserId(User);
        if (callerId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(callerId.Value, PermissionIdentifier.ViewUsers, _globalPermRepo)) {
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

// POST /api/users/{UserId}/permissions/global
public class GrantGlobalPermissionRequest {
    public Guid UserId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}

public class GrantGlobalPermissionEndpoint : Endpoint<GrantGlobalPermissionRequest> {
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly PermissionRepository _permissionRepo;

    public GrantGlobalPermissionEndpoint(UserGlobalPermissionRepository globalPermRepo,
        PermissionRepository permissionRepo) {
        _globalPermRepo = globalPermRepo;
        _permissionRepo = permissionRepo;
    }

    public override void Configure() {
        Post("/api/users/{UserId}/permissions/global");
    }

    public override async Task HandleAsync(GrantGlobalPermissionRequest req, CancellationToken ct) {
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

        _globalPermRepo.AddForUser(req.UserId, permission);
        await SendOkAsync(ct);
    }
}

// DELETE /api/users/{UserId}/permissions/global
public class RevokeGlobalPermissionRequest {
    public Guid UserId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}

public class RevokeGlobalPermissionEndpoint : Endpoint<RevokeGlobalPermissionRequest> {
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly PermissionRepository _permissionRepo;

    public RevokeGlobalPermissionEndpoint(UserGlobalPermissionRepository globalPermRepo,
        PermissionRepository permissionRepo) {
        _globalPermRepo = globalPermRepo;
        _permissionRepo = permissionRepo;
    }

    public override void Configure() {
        Delete("/api/users/{UserId}/permissions/global");
    }

    public override async Task HandleAsync(RevokeGlobalPermissionRequest req, CancellationToken ct) {
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

        _globalPermRepo.RemoveForUser(req.UserId, permission);
        await SendOkAsync(ct);
    }
}

// POST /api/users/{UserId}/permissions/chapter
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

// DELETE /api/users/{UserId}/permissions/chapter
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
