using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Anonymous_caller_gets_401() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        var response = await client.PutAsJsonAsync($"/api/motions/{motion.Id}", new MotionUpdateRequest {
            Id = motion.Id,
            Title = "New Title",
            TextMarkdown = "New body",
            AuthorName = "Author",
            AuthorEmail = "author@test.local"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task User_without_EditMotions_gets_403() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/motions/{motion.Id}", new MotionUpdateRequest {
            Id = motion.Id,
            Title = "New Title",
            TextMarkdown = "New body",
            AuthorName = "Author",
            AuthorEmail = "author@test.local"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Non_pending_motion_cannot_be_edited() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id, status: MotionApprovalStatus.Approved);
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapter.Id] = new[] { PermissionIdentifier.EditMotions }
        });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/motions/{motion.Id}", new MotionUpdateRequest {
            Id = motion.Id,
            Title = "New Title",
            TextMarkdown = "New body",
            AuthorName = "Author",
            AuthorEmail = "author@test.local"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Updates_changed_fields_and_writes_one_audit_entry_per_change() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id,
            title: "Old Title",
            text: "<p>Old body</p>",
            textMarkdown: "Old body",
            authorName: "Old Author",
            authorEmail: "old@test.local");
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapter.Id] = new[] { PermissionIdentifier.EditMotions }
        });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/motions/{motion.Id}", new MotionUpdateRequest {
            Id = motion.Id,
            Title = "New Title",
            TextMarkdown = "New body",
            AuthorName = "New Author",
            AuthorEmail = "new@test.local"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var saved = Db.Motions.Single(m => m.Id == motion.Id);
        await Assert.That(saved.Title).IsEqualTo("New Title");
        await Assert.That(saved.TextMarkdown).IsEqualTo("New body");
        await Assert.That(saved.Text.Contains("New body")).IsTrue();
        await Assert.That(saved.AuthorName).IsEqualTo("New Author");
        await Assert.That(saved.AuthorEmail).IsEqualTo("new@test.local");

        var updates = Db.AuditLogs
            .Where(a => a.EntityType == "Motion" && a.EntityId == motion.Id && a.Action == "Updated")
            .ToList();
        await Assert.That(updates.Count).IsEqualTo(4);
        await Assert.That(updates.Any(u => u.FieldName == "Title" && u.OldValue == "Old Title" && u.NewValue == "New Title")).IsTrue();
        await Assert.That(updates.Any(u => u.FieldName == "TextMarkdown" && u.OldValue == "Old body" && u.NewValue == "New body")).IsTrue();
        await Assert.That(updates.Any(u => u.FieldName == "AuthorName")).IsTrue();
        await Assert.That(updates.Any(u => u.FieldName == "AuthorEmail")).IsTrue();
    }

    [Test]
    public async Task Unchanged_fields_produce_no_audit_entries() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id,
            title: "Title",
            text: "Body",
            textMarkdown: "Body",
            authorName: "Author",
            authorEmail: "author@test.local");
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapter.Id] = new[] { PermissionIdentifier.EditMotions }
        });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/motions/{motion.Id}", new MotionUpdateRequest {
            Id = motion.Id,
            Title = "Title",
            TextMarkdown = "Body",
            AuthorName = "Author",
            AuthorEmail = "author@test.local"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updates = Db.AuditLogs.Count(a => a.EntityType == "Motion" && a.EntityId == motion.Id && a.Action == "Updated");
        await Assert.That(updates).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_400_when_linked_application_does_not_exist() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapter.Id] = new[] { PermissionIdentifier.EditMotions }
        });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/motions/{motion.Id}", new MotionUpdateRequest {
            Id = motion.Id,
            Title = "Title",
            TextMarkdown = "Body",
            AuthorName = "Author",
            AuthorEmail = "author@test.local",
            LinkedMembershipApplicationId = Guid.NewGuid()
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
