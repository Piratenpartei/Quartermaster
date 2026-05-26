using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
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
}

public class UserDetailEndpoint : Endpoint<UserDetailRequest, UserDetailResponse> {
    private readonly UserRepository _userRepo;
    private readonly PermissionContext _perms;

    public UserDetailEndpoint(UserRepository userRepo, PermissionContext perms) {
        _userRepo = userRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/{Id}");
    }

    public override async Task HandleAsync(UserDetailRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewUsers)) {
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
            LastName = user.LastName
        }, cancellation: ct);
    }
}
