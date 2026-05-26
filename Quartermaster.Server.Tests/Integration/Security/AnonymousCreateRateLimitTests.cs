using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Security;

public class AnonymousCreateRateLimitTests : IntegrationTestBase {
    private void SetAnonymousCreatePermits(string value) {
        Db.SystemOptions
            .Where(o => o.Identifier == "auth.ratelimit.anonymous_create_permits" && o.ChapterId == null)
            .Set(o => o.Value, value)
            .Update();
    }

    [Test]
    public async Task Sixth_request_in_window_returns_429() {
        var chapter = Builder.SeedChapter("RL");
        SetAnonymousCreatePermits("5");

        // Fresh factory so a brand-new rate limiter is built and picks up the strict Option value
        // when it creates the partition for this test's IP.
        using var strict = Factory.WithWebHostBuilder(_ => { });
        using var client = strict.CreateClient();
        await AttachAntiforgeryTokenAsync(client);

        for (var i = 0; i < 5; i++) {
            var ok = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
                ChapterId = chapter.Id,
                AuthorName = $"User{i}",
                AuthorEmail = $"u{i}@example.com",
                Title = $"Title {i}",
                Text = "x"
            });
            await Assert.That(ok.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        var rejected = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Spammer",
            AuthorEmail = "spam@example.com",
            Title = "Overflow",
            Text = "x"
        });
        await Assert.That((int)rejected.StatusCode).IsEqualTo(429);
    }

    [Test]
    public async Task Bucket_is_shared_across_all_three_anonymous_endpoints() {
        var chapter = Builder.SeedChapter("RL-shared");
        SetAnonymousCreatePermits("2");

        using var strict = Factory.WithWebHostBuilder(_ => { });
        using var client = strict.CreateClient();
        await AttachAntiforgeryTokenAsync(client);

        var first = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "A", AuthorEmail = "a@x.com", Title = "T1", Text = "x"
        });
        var second = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "B", AuthorEmail = "b@x.com", Title = "T2", Text = "x"
        });
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Third request on a *different* anonymous endpoint shares the same bucket → 429.
        var third = await client.PostAsJsonAsync("/api/dueselector", new {
            FirstName = "x", LastName = "y"
        });
        await Assert.That((int)third.StatusCode).IsEqualTo(429);
    }
}
