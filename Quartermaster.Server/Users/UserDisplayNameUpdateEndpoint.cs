using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class UserDisplayNameUpdateEndpoint : Endpoint<DisplayNameUpdateRequest> {
    private readonly UserRepository _userRepo;
    private readonly PermissionContext _perms;

    public UserDisplayNameUpdateEndpoint(UserRepository userRepo, PermissionContext perms) {
        _userRepo = userRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/users/settings/display-name");
    }

    public override async Task HandleAsync(DisplayNameUpdateRequest req, CancellationToken ct) {
        var user = _perms.UserId == null ? null : _userRepo.Get(_perms.UserId.Value);
        if (user == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (req.Mode == DisplayNameMode.Username && !user.HasUsername()) {
            ThrowError(I18nKey.Error.User.DisplayNameUsernameMissing);
            return;
        }

        _userRepo.SetDisplayNameMode(user.Id, req.Mode);
        await SendOkAsync(ct);
    }
}
