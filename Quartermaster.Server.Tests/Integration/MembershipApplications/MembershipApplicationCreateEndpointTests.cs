using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.MembershipApplications;
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
            EMail = "alice@test.local",
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
    public async Task Persists_application_with_pending_status() {
        var chapter = Builder.SeedChapter("C");
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
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
        dto.EMail = "not-an-email";
        var response = await client.PostAsJsonAsync("/api/membershipapplications", dto);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Creates_linked_motion_when_chapter_provided() {
        var chapter = Builder.SeedChapter("C");
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var savedApp = Db.MembershipApplications.First(a => a.FirstName == "Alice");
        var motion = Db.Motions.FirstOrDefault(m => m.LinkedMembershipApplicationId == savedApp.Id);
        await Assert.That(motion).IsNotNull();
    }
}
