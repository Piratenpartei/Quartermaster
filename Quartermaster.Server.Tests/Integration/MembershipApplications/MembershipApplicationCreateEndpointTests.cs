using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.MembershipApplications;

public class MembershipApplicationCreateEndpointTests : IntegrationTestBase {
    private MembershipApplicationDTO ValidDto(Guid? chapterId = null) {
        return new MembershipApplicationDTO {
            FirstName = "Alice",
            LastName = "Applicant",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Citizenship = "DE",
            Email = "alice@test.local",
            PhoneNumber = "0123456789",
            AddressStreet = "Teststr.",
            AddressHouseNbr = "1",
            AddressPostCode = "12345",
            AddressCity = "Testcity",
            AddressAdministrativeDivisionId = chapterId == null ? null : Builder.SeedDivisionFor(chapterId.Value),
            ConformityDeclarationAccepted = true,
            ApplicationText = "I want to join.",
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    private async Task<HttpResponseMessage> PostWithCode(HttpClient client, MembershipApplicationDTO dto) {
        dto.EmailVerificationCode = IssueEmailCode(dto.Email);
        return await client.PostAsJsonAsync("/api/membershipapplications", dto);
    }

    [Test]
    public async Task Anonymous_can_submit_application() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await PostWithCode(client, ValidDto(chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Refused_without_setup() {
        var chapter = Builder.SeedChapter("C");
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        var response = await PostWithCode(client, ValidDto(chapter.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Admin.Application.NotSetUp);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Hand_entry_refused_without_setup() {
        var chapter = Builder.SeedChapter("C");
        Db.SystemOptions.Where(o => o.Identifier == PublicFormOptions.MembershipApplication && o.ChapterId == null).Set(o => o.Value, "false").Update();
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.ProcessApplications] });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Admin.Application.NotSetUp);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Accepted_with_setup() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Anonymous_submit_stores_application_without_pending_row() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await PostWithCode(client, ValidDto(chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(1);
        await Assert.That(Db.PendingSubmissions.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Persists_application_with_pending_status() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        await PostWithCode(client, ValidDto(chapter.Id));
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
    public async Task Creates_linked_motion_when_chapter_provided() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        await PostWithCode(client, ValidDto(chapter.Id));
        var savedApp = Db.MembershipApplications.First(a => a.FirstName == "Alice");
        var motion = Db.Motions.FirstOrDefault(m => m.LinkedMembershipApplicationId == savedApp.Id);
        await Assert.That(motion).IsNotNull();
    }

    [Test]
    public async Task Authenticated_caller_creates_application_directly_without_pending_row() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
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

    private static DueSelectionDTO ReducedDues(decimal amount) {
        return new DueSelectionDTO {
            FirstName = "Alice",
            LastName = "Applicant",
            Email = "alice@test.local",
            SelectedValuation = SelectedValuation.Reduced,
            ReducedAmount = amount,
            ReducedJustification = "Studium",
            ReducedTimeSpan = ReducedTimeSpan.OneYear,
            PaymentSchedule = PaymentSchedule.Annual
        };
    }

    [Test]
    public async Task Stores_country_and_other_parties() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var dto = ValidDto(chapter.Id);
        dto.AddressCountry = "Österreich";
        dto.IsMemberOfAnotherParty = true;
        dto.OtherParties = "Volt";

        await PostWithCode(client, dto);

        var saved = Db.MembershipApplications.Single();
        await Assert.That(saved.AddressCountry).IsEqualTo("Österreich");
        await Assert.That(saved.OtherParties).IsEqualTo("Volt");
    }

    [Test]
    public async Task Address_in_germany_stores_no_country() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        await client.PostAsJsonAsync("/api/membershipapplications", ValidDto(chapter.Id));

        await Assert.That(Db.MembershipApplications.Single().AddressCountry).IsNull();
    }

    [Test]
    public async Task Reduction_outside_the_range_is_rejected() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var dto = ValidDto(chapter.Id);
        dto.DueSelection = ReducedDues(100);

        var response = await PostWithCode(client, dto);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Reduction_without_classification_records_the_default_due() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var dto = ValidDto(chapter.Id);
        dto.DueSelection = ReducedDues(24);

        var response = await client.PostAsJsonAsync("/api/membershipapplications", dto);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var application = Db.MembershipApplications.Single();
        var dues = Db.DueSelections.Single(d => d.Id == application.DueSelectionId);
        await Assert.That(dues.SelectedDue).IsEqualTo(DueSelectionRates.RejectedReductionDue);
    }
}
