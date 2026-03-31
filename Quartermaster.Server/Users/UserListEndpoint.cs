using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data;

namespace Quartermaster.Server.Users;

public class UserListItem {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public class UserListEndpoint : EndpointWithoutRequest<List<UserListItem>> {
    private readonly DbContext _context;

    public UserListEndpoint(DbContext context) {
        _context = context;
    }

    public override void Configure() {
        Get("/api/users");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var users = _context.Users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new UserListItem {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName
            })
            .ToList();

        await SendAsync(users, cancellation: ct);
    }
}
