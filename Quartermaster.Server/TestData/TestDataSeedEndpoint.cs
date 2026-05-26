using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TestData;

public class TestDataSeedResponse {
    public int Created { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Dev-only seeder triggered from the Blazor user-settings page (button is also DEBUG-only).
/// <c>DontRegister</c> elides the route entirely in Release builds; no test coverage is
/// warranted because the endpoint cannot be reached in production.
/// </summary>
public class TestDataSeedEndpoint : EndpointWithoutRequest<TestDataSeedResponse> {
    private readonly DbContext _context;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public TestDataSeedEndpoint(DbContext context, ChapterRepository chapterRepo,
        PermissionContext perms) {
        _context = context;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/testdata/seed");
#if !DEBUG
        DontRegister();
#endif
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var seeder = new TestDataSeeder(_context, _chapterRepo);
        var created = seeder.Seed();

        await SendAsync(new TestDataSeedResponse {
            Created = created,
            Message = $"Seeded {created} test records."
        }, cancellation: ct);
    }
}
