using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class UserDeleteRequest {
    public Guid Id { get; set; }
}

public class UserDeleteEndpoint : Endpoint<UserDeleteRequest> {
    private readonly UserRepository _userRepo;
    private readonly TokenRepository _tokenRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public UserDeleteEndpoint(
        UserRepository userRepo,
        TokenRepository tokenRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _userRepo = userRepo;
        _tokenRepo = tokenRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Delete("/api/users/{Id}");
    }

    public override async Task HandleAsync(UserDeleteRequest req, CancellationToken ct) {
        var callerId = EndpointAuthorizationHelper.GetUserId(User);
        if (callerId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(callerId.Value, PermissionIdentifier.DeleteUsers, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        // Prevent self-deletion
        if (callerId.Value == req.Id) {
            AddError("Id", "Der eigene Benutzer kann nicht gelöscht werden.");
            await SendErrorsAsync(400, ct);
            return;
        }

        var user = _userRepo.GetById(req.Id);
        if (user == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        _tokenRepo.DeleteAllForUser(user.Id);
        _userRepo.SoftDelete(user.Id);

        await SendOkAsync(ct);
    }
}
