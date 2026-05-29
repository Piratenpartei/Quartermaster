using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Submissions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionCreateEndpointTests : IntegrationTestBase {
    private async Task<HttpClient> AnonymousClientWithCsrfAsync() {
        var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    [Test]
    public async Task Submit_stashes_pending_and_does_not_create_motion() {
        var chapter = Builder.SeedChapter("Chapter");
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Jane Author",
            AuthorEmail = "jane@example.com",
            Title = "My Motion",
            Text = "Some motion text."
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var accepted = await response.Content.ReadFromJsonAsync<SubmissionAcceptedResponse>();
        await Assert.That(accepted!.Email).IsEqualTo("jane@example.com");

        // Spam barrier: nothing in the live table until confirmed.
        await Assert.That(Db.Motions.Count()).IsEqualTo(0);
        await Assert.That(Db.PendingSubmissions.Count(p => p.ConfirmedAt == null)).IsEqualTo(1);
    }

    [Test]
    public async Task Confirm_materializes_motion() {
        var chapter = Builder.SeedChapter("Chapter");
        using var client = await AnonymousClientWithCsrfAsync();
        await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Jane Author",
            AuthorEmail = "jane@example.com",
            Title = "My Motion",
            Text = "Some motion text."
        });

        await ConfirmAllPendingSubmissionsAsync();

        var motion = Db.Motions.Single();
        await Assert.That(motion.Title).IsEqualTo("My Motion");
        await Assert.That(motion.ChapterId).IsEqualTo(chapter.Id);
        await Assert.That(motion.IsPublic).IsFalse();
    }

    [Test]
    public async Task Returns_400_when_chapter_id_empty() {
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = Guid.Empty,
            AuthorName = "Jane",
            AuthorEmail = "jane@example.com",
            Title = "Title",
            Text = "Text"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_chapter_does_not_exist() {
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = Guid.NewGuid(),
            AuthorName = "Jane",
            AuthorEmail = "jane@example.com",
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
            AuthorEmail = "notanemail",
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
            AuthorEmail = "jane@example.com",
            Title = "",
            Text = "Text"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Sanitizes_markdown_text_strips_links_on_confirm() {
        var chapter = Builder.SeedChapter();
        using var client = await AnonymousClientWithCsrfAsync();
        await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Jane",
            AuthorEmail = "jane@example.com",
            Title = "Title",
            Text = "This contains [a link](https://evil.example.com) inline."
        });
        await ConfirmAllPendingSubmissionsAsync();

        var motion = Db.Motions.Single();
        await Assert.That(motion.Text.Contains("href=")).IsFalse();
        await Assert.That(motion.Text.Contains("evil.example.com")).IsFalse();
    }

    [Test]
    public async Task Authenticated_caller_creates_motion_directly_without_pending_row() {
        var chapter = Builder.SeedChapter("Chapter");
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Officer Olga",
            AuthorEmail = "olga@test.local",
            Title = "Direct motion",
            Text = "Body."
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var accepted = await response.Content.ReadFromJsonAsync<SubmissionAcceptedResponse>();
        await Assert.That(accepted!.RequiresConfirmation).IsFalse();
        await Assert.That(accepted.CreatedEntityId).IsNotNull();
        await Assert.That(Db.Motions.Single().Id).IsEqualTo(accepted.CreatedEntityId!.Value);
        await Assert.That(Db.PendingSubmissions.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Renders_markdown_paragraph_to_html_on_confirm() {
        var chapter = Builder.SeedChapter();
        using var client = await AnonymousClientWithCsrfAsync();
        await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapter.Id,
            AuthorName = "Jane",
            AuthorEmail = "jane@example.com",
            Title = "Title",
            Text = "Hello **world**"
        });
        await ConfirmAllPendingSubmissionsAsync();

        var motion = Db.Motions.Single();
        await Assert.That(motion.Text.Contains("<strong>")).IsTrue();
    }
}
