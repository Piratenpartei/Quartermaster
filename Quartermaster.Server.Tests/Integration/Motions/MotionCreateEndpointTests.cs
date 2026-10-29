using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
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

    private async Task<HttpResponseMessage> SubmitAsMemberAsync(Guid chapterId) {
        var (_, token) = Builder.SeedAuthenticatedUser(email: "member@test.local");
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapterId,
            AuthorName = "Member Mia",
            AuthorEmail = "member@test.local",
            Title = "Routed motion",
            Text = "Body."
        });
    }

    [Test]
    public async Task Motion_for_unable_chapter_goes_to_parent() {
        var parent = Builder.SeedChapter("Kreis");
        var marked = Builder.SeedChapter("Ortsverband", parentChapterId: parent.Id, unableToAct: true);
        var (parentOfficer, _) = Builder.SeedAuthenticatedUser(
            email: "parent@test.local",
            chapterPermissions: new() { [parent.Id] = new[] { PermissionIdentifier.EditMotions } });
        Builder.SeedAuthenticatedUser(
            email: "marked@test.local",
            chapterPermissions: new() { [marked.Id] = new[] { PermissionIdentifier.EditMotions } });

        var response = await SubmitAsMemberAsync(marked.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var motion = Db.Motions.Single();
        await Assert.That(motion.ChapterId).IsEqualTo(parent.Id);
        await Assert.That(motion.OnBehalfOfChapterId).IsEqualTo(marked.Id);
        await Assert.That(motion.OnBehalfOfReason).IsEqualTo(ChapterMark.UnableToAct);
        var recipients = Db.NotificationLogs.Where(l => l.TriggerId == "motion_submitted").Select(l => l.RecipientUserId).ToList();
        await Assert.That(recipients).IsEquivalentTo(new Guid?[] { parentOfficer.Id });
    }

    [Test]
    public async Task Motion_for_chapter_without_member_administration_stays() {
        var parent = Builder.SeedChapter("Kreis");
        var marked = Builder.SeedChapter("Ortsverband", parentChapterId: parent.Id, noMemberAdministration: true);

        var response = await SubmitAsMemberAsync(marked.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var motion = Db.Motions.Single();
        await Assert.That(motion.ChapterId).IsEqualTo(marked.Id);
        await Assert.That(motion.OnBehalfOfChapterId).IsNull();
        await Assert.That(motion.OnBehalfOfReason).IsNull();
    }
}
