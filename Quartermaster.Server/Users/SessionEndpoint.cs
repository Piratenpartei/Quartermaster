using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Users;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class SessionEndpoint : EndpointWithoutRequest<LoginResponse> {
    private readonly UserRepository _userRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly PermissionContext _perms;

    public SessionEndpoint(
        UserRepository userRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        PermissionContext perms) {
        _userRepo = userRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/session");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var user = _userRepo.Get(userId.Value);
        if (user == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var globalPermissions = _globalPermRepo.GetForUser(user.Id)
            .Select(p => p.Identifier)
            .ToList();

        var chapterPermissions = _chapterPermRepo.GetAllForUser(user.Id);

        await SendAsync(new LoginResponse {
            User = new LoginUserInfo {
                Id = user.Id,
                Username = user.Username ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                DisplayName = user.DisplayName(),
                Email = user.Email
            },
            Permissions = new LoginPermissions {
                Global = globalPermissions,
                Chapters = chapterPermissions.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value)
            }
        }, cancellation: ct);
    }
}
