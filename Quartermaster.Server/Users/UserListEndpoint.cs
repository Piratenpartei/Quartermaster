using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class UserListEndpoint : EndpointWithoutRequest<List<UserListItem>> {
    private readonly DbContext _context;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public UserListEndpoint(DbContext context, UserGlobalPermissionRepository globalPermRepo) {
        _context = context;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/users");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewUsers, _globalPermRepo)) {
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
