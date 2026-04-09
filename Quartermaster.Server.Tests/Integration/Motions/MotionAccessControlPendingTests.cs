using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

/// <summary>
/// Tests for motion access control: non-public motions require <c>ViewMotions</c>
/// permission (chapter-scoped, with inheritance). Anonymous users and users without
/// the permission see only public motions.
/// </summary>
public class MotionAccessControlPendingTests : IntegrationTestBase {
    [Test]
    public async Task List_should_not_return_private_motions_to_anonymous_even_with_IncludeNonPublic() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedMotion(chapter.Id, title: "Public");
        var nonPublic = Builder.SeedMotion(chapter.Id, title: "Hidden");
        Db.Motions.Where(m => m.Id == nonPublic.Id).Set(m => m.IsPublic, false).Update();

        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/motions?IncludeNonPublic=true");
        var result = await response.Content.ReadFromJsonAsync<MotionListResponse>();
        // Anonymous users must never see non-public motions, even when flag is set.
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Title).IsEqualTo("Public");
    }

    [Test]
    public async Task List_should_return_private_motions_to_user_with_ViewMotions_on_chapter() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedMotion(chapter.Id, title: "Public");
        var nonPublic = Builder.SeedMotion(chapter.Id, title: "Hidden");
        Db.Motions.Where(m => m.Id == nonPublic.Id).Set(m => m.IsPublic, false).Update();

        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewMotions } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/motions?IncludeNonPublic=true");
        var result = await response.Content.ReadFromJsonAsync<MotionListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Detail_should_return_404_for_private_motion_to_anonymous() {
        var chapter = Builder.SeedChapter("Chapter");
        var motion = Builder.SeedMotion(chapter.Id, title: "Secret");
        Db.Motions.Where(m => m.Id == motion.Id).Set(m => m.IsPublic, false).Update();

        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions/{motion.Id}");
        // Either 404 (don't reveal existence) or 403 is acceptable here — not 200.
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Detail_should_return_404_for_private_motion_to_user_without_ViewMotions() {
        var chapter = Builder.SeedChapter("Chapter");
        var motion = Builder.SeedMotion(chapter.Id, title: "Secret");
        Db.Motions.Where(m => m.Id == motion.Id).Set(m => m.IsPublic, false).Update();

        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/motions/{motion.Id}");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Detail_should_return_200_for_private_motion_to_user_with_ViewMotions_on_chapter() {
        var chapter = Builder.SeedChapter("Chapter");
        var motion = Builder.SeedMotion(chapter.Id, title: "Secret");
        Db.Motions.Where(m => m.Id == motion.Id).Set(m => m.IsPublic, false).Update();

        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewMotions } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/motions/{motion.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Detail_still_returns_200_for_public_motion_to_anonymous() {
        // Regression guard — don't break public access while fixing private access.
        var chapter = Builder.SeedChapter("Chapter");
        var motion = Builder.SeedMotion(chapter.Id, title: "Public");
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions/{motion.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
