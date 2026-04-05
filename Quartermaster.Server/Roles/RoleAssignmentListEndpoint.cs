using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleAssignmentListEndpoint : EndpointWithoutRequest<List<UserRoleAssignmentDTO>> {
    private readonly RoleRepository _roleRepo;
    private readonly UserRepository _userRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public RoleAssignmentListEndpoint(
        RoleRepository roleRepo,
        UserRepository userRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _roleRepo = roleRepo;
        _userRepo = userRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/roleassignments");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ManageRoles, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var assignments = _roleRepo.GetAllAssignments();
        var roles = _roleRepo.GetAll().ToDictionary(r => r.Id);
        var userIds = assignments.Select(a => a.UserId).Distinct().ToList();
        var users = userIds.ToDictionary(uid => uid, uid => _userRepo.GetById(uid));
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var dtos = assignments.Select(a => {
            roles.TryGetValue(a.RoleId, out var role);
            users.TryGetValue(a.UserId, out var user);
            return new UserRoleAssignmentDTO {
                Id = a.Id,
                UserId = a.UserId,
                UserDisplayName = BuildUserDisplayName(user),
                RoleId = a.RoleId,
                RoleName = role?.Name ?? "",
                RoleScope = role != null ? (int)role.Scope : 0,
                ChapterId = a.ChapterId,
                ChapterName = a.ChapterId.HasValue && chapters.TryGetValue(a.ChapterId.Value, out var cn) ? cn : null
            };
        }).OrderBy(d => d.UserDisplayName).ThenBy(d => d.RoleName).ToList();

        await SendAsync(dtos, cancellation: ct);
    }

    private static string BuildUserDisplayName(User? user) {
        if (user == null)
            return "";
        if (!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName))
            return $"{user.FirstName} {user.LastName}";
        return user.Username ?? user.EMail ?? "";
    }
}
