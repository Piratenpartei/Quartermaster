using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionCreateEndpointTests : IntegrationTestBase {
    private async Task<HttpClient> AnonymousClientWithCsrfAsync() {
        var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    [Test]
    public async Task Happy_path_creates_motion_and_persists() {
        var chapter = Builder.SeedChapter("Chapter");
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Jane Author",
            AuthorEMail = "jane@example.com",
            Title = "My Motion",
            Text = "Some motion text."
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MotionDTO>();
        await Assert.That(dto!.Title).IsEqualTo("My Motion");
        var persisted = Db.Motions.FirstOrDefault(m => m.Id == dto.Id);
        await Assert.That(persisted).IsNotNull();
        await Assert.That(persisted!.ChapterId).IsEqualTo(chapter.Id);
    }

    [Test]
    public async Task Returns_400_when_chapter_id_empty() {
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = Guid.Empty,
            AuthorName = "Jane",
            AuthorEMail = "jane@example.com",
            Title = "Title",
            Text = "Text"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_email_missing_at_sign() {
        var chapter = Builder.SeedChapter();
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Jane",
            AuthorEMail = "notanemail",
            Title = "Title",
            Text = "Text"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_title_empty() {
        var chapter = Builder.SeedChapter();
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Jane",
            AuthorEMail = "jane@example.com",
            Title = "",
            Text = "Text"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Sanitizes_markdown_text_strips_links() {
        var chapter = Builder.SeedChapter();
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Jane",
            AuthorEMail = "jane@example.com",
            Title = "Title",
            Text = "This contains [a link](https://evil.example.com) inline."
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MotionDTO>();
        var persisted = Db.Motions.FirstOrDefault(m => m.Id == dto!.Id);
        await Assert.That(persisted).IsNotNull();
        // Strict profile should strip the anchor tag; href should not be present.
        await Assert.That(persisted!.Text.Contains("href=")).IsFalse();
        await Assert.That(persisted.Text.Contains("evil.example.com")).IsFalse();
    }

    [Test]
    public async Task Renders_markdown_paragraph_to_html() {
        var chapter = Builder.SeedChapter();
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Jane",
            AuthorEMail = "jane@example.com",
            Title = "Title",
            Text = "Hello **world**"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MotionDTO>();
        var persisted = Db.Motions.FirstOrDefault(m => m.Id == dto!.Id);
        await Assert.That(persisted!.Text.Contains("<strong>")).IsTrue();
    }
}
