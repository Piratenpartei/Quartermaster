using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Users;
using Quartermaster.Data.Members;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class SessionEndpoint : EndpointWithoutRequest<LoginResponse> {
    private readonly UserRepository _userRepo;
    private readonly MemberRepository _memberRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly PermissionContext _perms;

    public SessionEndpoint(
        UserRepository userRepo,
        MemberRepository memberRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        PermissionContext perms) {
        _userRepo = userRepo;
        _memberRepo = memberRepo;
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
            User = user.ToLoginUserInfo(_memberRepo.GetByUserId(user.Id)),
            Permissions = new LoginPermissions {
                Global = globalPermissions,
                Chapters = chapterPermissions.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value)
            }
        }, cancellation: ct);
    }
}
