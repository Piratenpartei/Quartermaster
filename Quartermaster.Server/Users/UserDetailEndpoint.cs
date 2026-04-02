using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Users;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class UserDetailRequest {
    public Guid Id { get; set; }
}

public class UserDetailResponse {
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string EMail { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public class UserDetailEndpoint : Endpoint<UserDetailRequest, UserDetailResponse> {
    private readonly UserRepository _userRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public UserDetailEndpoint(UserRepository userRepo, UserGlobalPermissionRepository globalPermRepo) {
        _userRepo = userRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/users/{Id}");
    }

    public override async Task HandleAsync(UserDetailRequest req, CancellationToken ct) {
        var callerId = EndpointAuthorizationHelper.GetUserId(User);
        if (callerId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(callerId.Value, PermissionIdentifier.ViewUsers, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var user = _userRepo.GetById(req.Id);
        if (user == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new UserDetailResponse {
            Id = user.Id,
            Username = user.Username ?? "",
            EMail = user.EMail,
            FirstName = user.FirstName,
            LastName = user.LastName
        }, cancellation: ct);
    }
}
