using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.TestData;

public class TestDataSeedResponse {
    public int Created { get; set; }
    public string Message { get; set; } = "";
}

public class TestDataSeedEndpoint : EndpointWithoutRequest<TestDataSeedResponse> {
    private readonly DbContext _context;
    private readonly ChapterRepository _chapterRepo;

    public TestDataSeedEndpoint(DbContext context, ChapterRepository chapterRepo) {
        _context = context;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/testdata/seed");
        AllowAnonymous();
#if !DEBUG
        DontRegister();
#endif
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var seeder = new TestDataSeeder(_context, _chapterRepo);
        var created = seeder.Seed();

        await SendAsync(new TestDataSeedResponse {
            Created = created,
            Message = $"Seeded {created} test records."
        }, cancellation: ct);
    }
}
