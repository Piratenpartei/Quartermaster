using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.AuditLog;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AuditLog;

public class AuditLogEndpointTests : IntegrationTestBase {
    private void SeedAudit(string entityType, Guid entityId, string action = "Created") {
        Db.Insert(new Quartermaster.Data.AuditLog.AuditEntry {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Timestamp = DateTime.UtcNow
        });
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_audit() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_empty_list_when_no_entries_match() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAudit });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<AuditLogDTO>>();
        await Assert.That(list!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_audit_entries_for_matching_entity() {
        var entityId = Guid.NewGuid();
        SeedAudit("User", entityId, "Created");
        SeedAudit("User", entityId, "Updated");
        SeedAudit("User", Guid.NewGuid(), "Created");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAudit });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={entityId}");
        var list = await response.Content.ReadFromJsonAsync<List<AuditLogDTO>>();
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    private async Task<HttpStatusCode> StatusFor(string entityType, Guid entityId, Guid chapterId, string permission) {
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapterId] = new[] { permission }
        });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType={entityType}&EntityId={entityId}");
        return response.StatusCode;
    }

    [Test]
    public async Task Chapter_edit_motions_permission_returns_motion_audit_entries() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        SeedAudit("Motion", motion.Id, "Created");
        SeedAudit("Motion", motion.Id, "Updated");
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapter.Id] = new[] { PermissionIdentifier.EditMotions }
        });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType=Motion&EntityId={motion.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<AuditLogDTO>>();
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Chapter_view_motions_permission_is_not_enough_for_motion_audit() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);

        var status = await StatusFor("Motion", motion.Id, chapter.Id, PermissionIdentifier.ViewMotions);

        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Chapter_edit_events_permission_returns_event_audit() {
        var chapter = Builder.SeedChapter();
        var ev = Builder.SeedEvent(chapter.Id);

        var status = await StatusFor("Event", ev.Id, chapter.Id, PermissionIdentifier.EditEvents);

        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Chapter_view_events_permission_is_not_enough_for_event_audit() {
        var chapter = Builder.SeedChapter();
        var ev = Builder.SeedEvent(chapter.Id);

        var status = await StatusFor("Event", ev.Id, chapter.Id, PermissionIdentifier.ViewEvents);

        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Edit_events_on_other_chapter_is_not_enough_for_event_audit() {
        var chapter = Builder.SeedChapter();
        var other = Builder.SeedChapter();
        var ev = Builder.SeedEvent(chapter.Id);

        var status = await StatusFor("Event", ev.Id, other.Id, PermissionIdentifier.EditEvents);

        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Chapter_edit_meetings_permission_returns_meeting_audit() {
        var chapter = Builder.SeedChapter();
        var meeting = Builder.SeedMeeting(chapter.Id);

        var status = await StatusFor("Meeting", meeting.Id, chapter.Id, PermissionIdentifier.EditMeetings);

        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Chapter_view_meetings_permission_is_not_enough_for_meeting_audit() {
        var chapter = Builder.SeedChapter();
        var meeting = Builder.SeedMeeting(chapter.Id);

        var status = await StatusFor("Meeting", meeting.Id, chapter.Id, PermissionIdentifier.ViewMeetings);

        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Member_audit_needs_view_audit_even_with_edit_members() {
        var chapter = Builder.SeedChapter();
        var member = Builder.SeedMember(chapterId: chapter.Id);

        var status = await StatusFor("Member", member.Id, chapter.Id, PermissionIdentifier.EditMembers);

        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Missing_event_returns_403_for_chapter_user() {
        var chapter = Builder.SeedChapter();

        var status = await StatusFor("Event", Guid.NewGuid(), chapter.Id, PermissionIdentifier.EditEvents);

        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Chapter_edit_permissions_do_not_grant_access_to_other_entity_types() {
        var chapter = Builder.SeedChapter();

        var status = await StatusFor("User", Guid.NewGuid(), chapter.Id, PermissionIdentifier.EditEvents);

        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Does_not_return_entries_for_different_entity_type() {
        var entityId = Guid.NewGuid();
        SeedAudit("User", entityId);
        SeedAudit("Chapter", entityId);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAudit });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={entityId}");
        var list = await response.Content.ReadFromJsonAsync<List<AuditLogDTO>>();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0].EntityType).IsEqualTo("User");
    }
}
