using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Roles;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleAssignmentCreateEndpoint : Endpoint<RoleAssignmentCreateRequest> {
    private readonly RoleRepository _roleRepo;
    private readonly UserRepository _userRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public RoleAssignmentCreateEndpoint(
        RoleRepository roleRepo,
        UserRepository userRepo,
        ChapterRepository chapterRepo,
        PermissionContext perms) {
        _roleRepo = roleRepo;
        _userRepo = userRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/roleassignments");
    }

    public override async Task HandleAsync(RoleAssignmentCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageRoles)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var role = _roleRepo.Get(req.RoleId);
        if (role == null) {
            ThrowError(I18nKey.Error.User.RoleAssignment.RoleNotFound);
            return;
        }
        var targetUser = _userRepo.Get(req.UserId);
        if (targetUser == null) {
            ThrowError(I18nKey.Error.User.RoleAssignment.UserNotFound);
            return;
        }

        if (role.Scope == RoleScope.ChapterScoped) {
            if (!req.ChapterId.HasValue) {
                ThrowError(I18nKey.Error.User.RoleAssignment.ChapterRequired);
                return;
            }
            if (_chapterRepo.Get(req.ChapterId.Value) == null) {
                ThrowError(I18nKey.Error.User.RoleAssignment.ChapterNotFound);
                return;
            }
        } else {
            if (req.ChapterId.HasValue) {
                ThrowError(I18nKey.Error.User.RoleAssignment.GlobalNoChapter);
                return;
            }
        }

        _roleRepo.Assign(req.UserId, req.RoleId, req.ChapterId);
        await SendOkAsync(ct);
    }
}
