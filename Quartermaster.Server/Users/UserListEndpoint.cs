using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class UserListEndpoint : EndpointWithoutRequest<List<UserListItem>> {
    private readonly DbContext _context;
    private readonly PermissionContext _perms;

    public UserListEndpoint(DbContext context, PermissionContext perms) {
        _context = context;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewUsers)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var users = _context.Users
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new UserListItem {
                Id = u.Id,
                Username = u.Username ?? "",
                FirstName = u.FirstName,
                LastName = u.LastName
            })
            .ToList();

        await SendAsync(users, cancellation: ct);
    }
}
