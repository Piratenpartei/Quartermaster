using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class UserDeleteRequest {
    public Guid Id { get; set; }
}

public class UserDeleteEndpoint : Endpoint<UserDeleteRequest> {
    private readonly UserRepository _userRepo;
    private readonly TokenRepository _tokenRepo;
    private readonly PermissionContext _perms;

    public UserDeleteEndpoint(
        UserRepository userRepo,
        TokenRepository tokenRepo,
        PermissionContext perms) {
        _userRepo = userRepo;
        _tokenRepo = tokenRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/users/{Id}");
    }

    public override async Task HandleAsync(UserDeleteRequest req, CancellationToken ct) {
        var callerId = _perms.UserId;
        if (callerId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.DeleteUsers)) {
            await SendForbiddenAsync(ct);
            return;
        }

        // Prevent self-deletion
        if (callerId.Value == req.Id) {
            AddError("Id", I18nKey.Error.User.DeleteSelfForbidden);
            await SendErrorsAsync(400, ct);
            return;
        }

        var user = _userRepo.Get(req.Id);
        if (user == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        _tokenRepo.DeleteAllForUser(user.Id);
        _userRepo.SoftDelete(user.Id);

        await SendOkAsync(ct);
    }
}
