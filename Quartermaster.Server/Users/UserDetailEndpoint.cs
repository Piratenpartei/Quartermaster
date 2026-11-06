using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Members;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class UserDetailRequest {
    public Guid Id { get; set; }
}

public class UserDetailResponse {
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    /// <summary>The member linked to this account; set only when the viewer may open it.</summary>
    public Guid? ViewableMemberId { get; set; }
}

public class UserDetailEndpoint : Endpoint<UserDetailRequest, UserDetailResponse> {
    private readonly UserRepository _userRepo;
    private readonly MemberRepository _memberRepo;
    private readonly PermissionContext _perms;
    private readonly DetailPageAccess _pageAccess;

    public UserDetailEndpoint(UserRepository userRepo, MemberRepository memberRepo, PermissionContext perms, DetailPageAccess pageAccess) {
        _userRepo = userRepo;
        _memberRepo = memberRepo;
        _perms = perms;
        _pageAccess = pageAccess;
    }

    public override void Configure() {
        Get("/api/users/{Id}");
    }

    public override async Task HandleAsync(UserDetailRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_pageAccess.CanViewUsers()) {
            await SendForbiddenAsync(ct);
            return;
        }

        var user = _userRepo.Get(req.Id);
        if (user == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new UserDetailResponse {
            Id = user.Id,
            Username = user.Username ?? "",
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            ViewableMemberId = ViewableMemberOf(user.Id)
        }, cancellation: ct);
    }

    private Guid? ViewableMemberOf(Guid userId) {
        var member = _memberRepo.GetByUserIdIncludingFloating(userId);
        if (member == null || !_pageAccess.CanViewMember(member))
            return null;
        return member.Id;
    }
}
