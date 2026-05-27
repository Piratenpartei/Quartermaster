using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionStatusEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = motion.Id,
            ApprovalStatus = MotionApprovalStatus.FormallyRejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_edit_motions_permission() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = motion.Id,
            ApprovalStatus = MotionApprovalStatus.FormallyRejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Toggle_IsPublic_persists_and_logs_audit() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id, isPublic: false);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = motion.Id,
            IsPublic = true
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var stored = Db.Motions.Single(m => m.Id == motion.Id);
        await Assert.That(stored.IsPublic).IsTrue();

        var audit = Db.AuditLogs.Where(a => a.EntityType == "Motion" && a.EntityId == motion.Id && a.FieldName == "IsPublic").ToList();
        await Assert.That(audit.Count).IsEqualTo(1);
        await Assert.That(audit[0].OldValue).IsEqualTo("False");
        await Assert.That(audit[0].NewValue).IsEqualTo("True");
    }

    [Test]
    public async Task Returns_403_when_toggling_IsPublic_without_permission() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id, isPublic: false);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = motion.Id,
            IsPublic = true
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_motion() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = Guid.NewGuid(),
            ApprovalStatus = MotionApprovalStatus.FormallyRejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_motion_id_empty() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = Guid.Empty,
            ApprovalStatus = MotionApprovalStatus.FormallyRejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Updates_status_to_formally_rejected() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = motion.Id,
            ApprovalStatus = MotionApprovalStatus.FormallyRejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.Motions.First(m => m.Id == motion.Id);
        await Assert.That(persisted.ApprovalStatus).IsEqualTo(MotionApprovalStatus.FormallyRejected);
    }

    [Test]
    public async Task Rejects_approved_or_rejected_status_with_400() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = motion.Id,
            ApprovalStatus = MotionApprovalStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Updates_is_realized_flag() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/status", new MotionStatusRequest {
            MotionId = motion.Id,
            IsRealized = true
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.Motions.First(m => m.Id == motion.Id);
        await Assert.That(persisted.IsRealized).IsTrue();
    }
}
