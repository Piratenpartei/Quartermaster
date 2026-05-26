using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class LoginLockoutUnlockEndpoint : Endpoint<LoginLockoutUnlockRequest> {
    private readonly LoginAttemptRepository _loginAttemptRepository;
    private readonly PermissionContext _perms;

    public LoginLockoutUnlockEndpoint(
        LoginAttemptRepository loginAttemptRepository,
        PermissionContext perms) {
        _loginAttemptRepository = loginAttemptRepository;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/users/lockouts/unlock");
    }

    public override async Task HandleAsync(LoginLockoutUnlockRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewUsers)) {
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
