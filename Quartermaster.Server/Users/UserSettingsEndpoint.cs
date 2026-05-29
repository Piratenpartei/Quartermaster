using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Users;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class UserSettingsEndpoint : EndpointWithoutRequest<UserSettingsDTO> {
    private readonly UserRepository _userRepo;
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionRepository _permissionRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly PermissionContext _perms;

    public UserSettingsEndpoint(
        UserRepository userRepo,
        MemberRepository memberRepo,
        ChapterRepository chapterRepo,
        PermissionRepository permissionRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        PermissionContext perms) {
        _userRepo = userRepo;
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
        _permissionRepo = permissionRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/settings");
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

        var allPermissions = _permissionRepo.GetAll().ToDictionary(p => p.Identifier, p => p.DisplayName);
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        // Global permissions with display names
        var globalPerms = _globalPermRepo.GetForUser(user.Id)
            .Select(p => new UserSettingsPermissionInfo {
                Identifier = p.Identifier,
                DisplayName = p.DisplayName
            })
            .ToList();

        // Chapter permissions grouped by chapter with display names
        var chapterPermsRaw = _chapterPermRepo.GetAllForUser(user.Id);
        var chapterPerms = chapterPermsRaw
            .Select(kvp => new UserSettingsChapterPermissions {
                ChapterName = chapters.TryGetValue(kvp.Key, out var name) ? name : kvp.Key.ToString(),
                Permissions = kvp.Value
                    .Select(id => new UserSettingsPermissionInfo {
                        Identifier = id,
                        DisplayName = allPermissions.TryGetValue(id, out var dn) ? dn : id
                    })
                    .ToList()
            })
            .ToList();

        // Linked member info
        UserSettingsMemberInfo? memberInfo = null;
        var member = _memberRepo.GetByUserId(user.Id);
        if (member != null) {
            memberInfo = new UserSettingsMemberInfo {
                MemberNumber = member.MemberNumber,
                ChapterName = member.ChapterId.HasValue && chapters.TryGetValue(member.ChapterId.Value, out var cn) ? cn : "",
                EntryDate = member.EntryDate,
                MembershipFee = member.MembershipFee,
                ReducedFee = member.ReducedFee,
                HasVotingRights = member.HasVotingRights,
                IsPending = member.IsPending,
                OpenFeeTotal = member.OpenFeeTotal
            };
        }

        await SendAsync(new UserSettingsDTO {
            User = new LoginUserInfo {
                Id = user.Id,
                Username = user.Username ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                DisplayName = user.DisplayName(),
                Email = user.Email
            },
            Member = memberInfo,
            GlobalPermissions = globalPerms,
            ChapterPermissions = chapterPerms
        }, cancellation: ct);
    }
}
