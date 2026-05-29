using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class MembershipApplicationLinkDivisionEndpointTests : IntegrationTestBase {
    private MembershipApplicationDTO ValidDto(Guid? chapterId) => new() {
        FirstName = "Mara",
        LastName = "Manual",
        DateOfBirth = new DateTime(1990, 1, 1),
        Citizenship = "DE",
        Email = "mara@test.local",
        PhoneNumber = "0123456789",
        AddressStreet = "Teststr.",
        AddressHouseNbr = "1",
        AddressPostCode = "30159",
        AddressCity = "Hannover",
        ChapterId = chapterId,
        ConformityDeclarationAccepted = true,
        ApplicationText = "Join.",
        EntryDate = DateTime.UtcNow.Date
    };

    [Test]
    public async Task Confirmed_application_without_chapter_enters_PendingDivisionLinking() {
        using var anon = AnonymousClient();
        await AttachAntiforgeryTokenAsync(anon);
        await anon.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapterId: null));
        await ConfirmAllPendingSubmissionsAsync();

        var app = Db.MembershipApplications.Single(a => a.FirstName == "Mara");
        await Assert.That(app.Status).IsEqualTo(ApplicationStatus.PendingDivisionLinking);
        await Assert.That(app.ChapterId).IsNull();
        await Assert.That(Db.Motions.Count(m => m.LinkedMembershipApplicationId == app.Id)).IsEqualTo(0);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == "application_submitted")).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_403_without_link_permission() {
        var app = Builder.SeedMembershipApplication(status: ApplicationStatus.PendingDivisionLinking);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var resp = await client.PostAsJsonAsync("/api/admin/membershipapplications/link-division",
            new { Id = app.Id, NotInGermany = true });
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Rejects_application_not_in_pending_linking_state() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Pending);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.LinkApplicationDivision });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var resp = await client.PostAsJsonAsync("/api/admin/membershipapplications/link-division",
            new { Id = app.Id, NotInGermany = true });
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_when_no_division_and_not_marked_foreign() {
        var app = Builder.SeedMembershipApplication(status: ApplicationStatus.PendingDivisionLinking);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.LinkApplicationDivision });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var resp = await client.PostAsJsonAsync("/api/admin/membershipapplications/link-division",
            new { Id = app.Id, NotInGermany = false });
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Linking_a_division_assigns_chapter_moves_to_pending_and_notifies() {
        var division = Builder.SeedAdminDivision("Niedersachsen");
        var chapter = Builder.SeedChapter("LV Niedersachsen", adminDivisionId: division.Id);
        var app = Builder.SeedMembershipApplication(status: ApplicationStatus.PendingDivisionLinking);
        // An officer who should be notified once the chapter is linked.
        Builder.SeedAuthenticatedUser(
            email: "officer@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ProcessApplications } });
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.LinkApplicationDivision });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var resp = await client.PostAsJsonAsync("/api/admin/membershipapplications/link-division",
            new { Id = app.Id, AdministrativeDivisionId = division.Id, NotInGermany = false });
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = Db.MembershipApplications.Single(a => a.Id == app.Id);
        await Assert.That(updated.Status).IsEqualTo(ApplicationStatus.Pending);
        await Assert.That(updated.ChapterId).IsEqualTo(chapter.Id);
        await Assert.That(updated.AddressAdministrativeDivisionId).IsEqualTo(division.Id);
        await Assert.That(Db.Motions.Count(m => m.LinkedMembershipApplicationId == app.Id)).IsEqualTo(1);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == "application_submitted")).IsEqualTo(1);
    }

    [Test]
    public async Task Marking_foreign_routes_to_root_chapter_with_no_division() {
        var root = Builder.SeedChapter("Piratenpartei Deutschland");
        var app = Builder.SeedMembershipApplication(status: ApplicationStatus.PendingDivisionLinking);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.LinkApplicationDivision });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var resp = await client.PostAsJsonAsync("/api/admin/membershipapplications/link-division",
            new { Id = app.Id, NotInGermany = true });
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = Db.MembershipApplications.Single(a => a.Id == app.Id);
        await Assert.That(updated.Status).IsEqualTo(ApplicationStatus.Pending);
        await Assert.That(updated.ChapterId).IsEqualTo(root.Id);
        await Assert.That(updated.AddressAdministrativeDivisionId).IsNull();
        await Assert.That(Db.Motions.Count(m => m.LinkedMembershipApplicationId == app.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Link_permission_holder_can_list_pending_linking_queue() {
        Builder.SeedMembershipApplication(status: ApplicationStatus.PendingDivisionLinking, firstName: "Queued");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.LinkApplicationDivision });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var resp = await client.GetAsync(
            $"/api/admin/membershipapplications?status={(int)ApplicationStatus.PendingDivisionLinking}&page=1&pageSize=50");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<MembershipApplicationListResponse>();
        await Assert.That(list!.Items.Any(i => i.FirstName == "Queued")).IsTrue();
    }
}
