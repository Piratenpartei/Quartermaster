using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Dashboard;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Dashboard;

public class DashboardEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Anonymous_gets_empty_widgets_but_public_events() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedEvent(chapter.Id, eventDate: DateTime.UtcNow.AddDays(10),
            status: EventStatus.Active, visibility: EventVisibility.Public);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/dashboard");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<DashboardDTO>();
        await Assert.That(dto!.PendingApplications).IsNull();
        await Assert.That(dto.PendingDueSelections).IsNull();
        await Assert.That(dto.OpenMotions).IsNull();
        await Assert.That(dto.UpcomingEvents).IsNotNull();
        await Assert.That(dto.UpcomingEvents!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Authenticated_user_without_permissions_sees_null_sections() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser();
        Builder.SeedMembershipApplication(chapter.Id);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/dashboard");
        var dto = await response.Content.ReadFromJsonAsync<DashboardDTO>();
        await Assert.That(dto!.PendingApplications).IsNull();
        await Assert.That(dto.PendingDueSelections).IsNull();
        await Assert.That(dto.OpenMotions).IsNull();
    }

    [Test]
    public async Task Applications_section_present_with_permission() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedMembershipApplication(chapter.Id, "A", "A");
        Builder.SeedMembershipApplication(chapter.Id, "B", "B");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewApplications });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/dashboard");
        var dto = await response.Content.ReadFromJsonAsync<DashboardDTO>();
        await Assert.That(dto!.PendingApplications).IsNotNull();
        await Assert.That(dto.PendingApplications!.TotalCount).IsEqualTo(2);
    }

    [Test]
    public async Task DueSelections_section_present_with_global_permission() {
        Builder.SeedDueSelection("A", "A");
        Builder.SeedDueSelection("B", "B");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewDueSelections });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/dashboard");
        var dto = await response.Content.ReadFromJsonAsync<DashboardDTO>();
        await Assert.That(dto!.PendingDueSelections).IsNotNull();
    }

    [Test]
    public async Task Motions_section_present_with_view_motions_permission() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedMotion(chapter.Id, "Motion 1");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewMotions });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/dashboard");
        var dto = await response.Content.ReadFromJsonAsync<DashboardDTO>();
        await Assert.That(dto!.OpenMotions).IsNotNull();
        await Assert.That(dto.OpenMotions!.TotalCount).IsEqualTo(1);
    }

    [Test]
    public async Task Upcoming_events_include_private_for_users_with_events_permission() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedEvent(chapter.Id, publicName: "Public Event",
            status: EventStatus.Active, visibility: EventVisibility.Public,
            eventDate: DateTime.UtcNow.AddDays(5));
        Builder.SeedEvent(chapter.Id, publicName: "Private Event",
            status: EventStatus.Active, visibility: EventVisibility.Private,
            eventDate: DateTime.UtcNow.AddDays(6));
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewEvents });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/dashboard");
        var dto = await response.Content.ReadFromJsonAsync<DashboardDTO>();
        await Assert.That(dto!.UpcomingEvents!.Count).IsEqualTo(2);
    }
}
