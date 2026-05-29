using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.MembershipApplications;

public class MembershipApplicationCreateEndpointTests : IntegrationTestBase {
    private MembershipApplicationDTO ValidDto(Guid? chapterId = null) {
        return new MembershipApplicationDTO {
            FirstName = "Alice",
            LastName = "Applicant",
            DateOfBirth = new DateTime(1990, 1, 1),
            Citizenship = "DE",
            Email = "alice@test.local",
            PhoneNumber = "0123456789",
            AddressStreet = "Teststr.",
            AddressHouseNbr = "1",
            AddressPostCode = "12345",
            AddressCity = "Testcity",
            ChapterId = chapterId,
            ConformityDeclarationAccepted = true,
            ApplicationText = "I want to join.",
            EntryDate = DateTime.UtcNow.Date
        };
    }

    [Test]
    public async Task Anonymous_can_submit_application() {
        var chapter = Builder.SeedChapter("C");
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Submit_stashes_pending_and_does_not_create_application() {
        var chapter = Builder.SeedChapter("C");
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
        await Assert.That(Db.PendingSubmissions.Count(p => p.ConfirmedAt == null)).IsEqualTo(1);
    }

    [Test]
    public async Task Persists_application_with_pending_status_on_confirm() {
        var chapter = Builder.SeedChapter("C");
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));
        await ConfirmAllPendingSubmissionsAsync();
        var saved = Db.MembershipApplications.First(a => a.FirstName == "Alice");
        await Assert.That(saved.Status).IsEqualTo(ApplicationStatus.Pending);
        await Assert.That(saved.ChapterId).IsEqualTo(chapter.Id);
    }

    [Test]
    public async Task Returns_400_when_conformity_not_accepted() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var dto = ValidDto();
        dto.ConformityDeclarationAccepted = false;
        var response = await client.PostAsJsonAsync("/api/membershipapplications", dto);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_required_fields_missing() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var dto = ValidDto();
        dto.FirstName = "";
        var response = await client.PostAsJsonAsync("/api/membershipapplications", dto);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_email_missing_at_sign() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var dto = ValidDto();
        dto.Email = "not-an-email";
        var response = await client.PostAsJsonAsync("/api/membershipapplications", dto);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Creates_linked_motion_when_chapter_provided_on_confirm() {
        var chapter = Builder.SeedChapter("C");
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));
        await ConfirmAllPendingSubmissionsAsync();
        var savedApp = Db.MembershipApplications.First(a => a.FirstName == "Alice");
        var motion = Db.Motions.FirstOrDefault(m => m.LinkedMembershipApplicationId == savedApp.Id);
        await Assert.That(motion).IsNotNull();
    }

    [Test]
    public async Task Authenticated_caller_creates_application_directly_without_pending_row() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var accepted = await response.Content.ReadFromJsonAsync<SubmissionAcceptedResponse>();
        await Assert.That(accepted!.RequiresConfirmation).IsFalse();
        await Assert.That(accepted.CreatedEntityId).IsNotNull();

        var saved = Db.MembershipApplications.Single();
        await Assert.That(saved.Id).IsEqualTo(accepted.CreatedEntityId!.Value);
        await Assert.That(saved.Status).IsEqualTo(ApplicationStatus.Pending);
        await Assert.That(Db.PendingSubmissions.Count()).IsEqualTo(0);
        await Assert.That(Db.Motions.Count(m => m.LinkedMembershipApplicationId == saved.Id)).IsEqualTo(1);
    }
}
