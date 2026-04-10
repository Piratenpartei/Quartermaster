using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class LoginLockoutUnlockEndpoint : Endpoint<LoginLockoutUnlockRequest> {
    private readonly LoginAttemptRepository _loginAttemptRepository;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public LoginLockoutUnlockEndpoint(
        LoginAttemptRepository loginAttemptRepository,
        UserGlobalPermissionRepository globalPermRepo) {
        _loginAttemptRepository = loginAttemptRepository;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/users/lockouts/unlock");
    }

    public override async Task HandleAsync(LoginLockoutUnlockRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewUsers, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.IpAddress) || string.IsNullOrWhiteSpace(req.UsernameOrEmail)) {
            ThrowError(I18nKey.Error.User.Login.UnlockIpAndUsernameRequired);
            return;
        }

        _loginAttemptRepository.ClearFailures(req.IpAddress, req.UsernameOrEmail);
        await SendOkAsync(ct);
    }
}
