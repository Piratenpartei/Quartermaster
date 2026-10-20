using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.Config;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.PublicForms;

public class PublicFormTests : IntegrationTestBase {
    private void SetOption(string identifier, Guid? chapterId, string value) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(identifier, chapterId, value);
    }

    private async Task<HttpClient> AnonymousWithCsrfAsync() {
        var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private MembershipApplicationDTO Application(Guid chapterId) {
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
            AddressAdministrativeDivisionId = Builder.SeedDivisionFor(chapterId),
            ConformityDeclarationAccepted = true,
            ApplicationText = "I want to join.",
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    private static DueSelectionDTO DueSelection() {
        return new DueSelectionDTO {
            FirstName = "Alice",
            LastName = "Anderson",
            Email = "alice@test.local",
            MemberNumber = 12345,
            SelectedValuation = SelectedValuation.OnePercentYearlyPay,
            YearlyIncome = 30000m,
            MonthlyIncomeGroup = 2500m,
            SelectedDue = 300m,
            ReducedAmount = 12m,
            ReducedJustification = "",
            ReducedTimeSpan = ReducedTimeSpan.OneYear,
            IsDirectDeposit = false,
            AccountHolder = "Alice Anderson",
            IBAN = "DE89370400440532013000",
            PaymentSchedule = PaymentSchedule.Annual
        };
    }

    private static MotionCreateRequest Motion(Guid chapterId) {
        return new MotionCreateRequest {
            ChapterId = chapterId,
            AuthorName = "Jane Author",
            AuthorEmail = "jane@example.com",
            Title = "My Motion",
            Text = "Some motion text."
        };
    }

    [Test]
    public async Task Application_refused_while_switched_off() {
        var chapter = Builder.SeedChapter("C");
        SetOption(PublicFormOptions.MembershipApplication, null, "false");
        using var client = await AnonymousWithCsrfAsync();

        var response = await client.PostAsJsonAsync("/api/membershipapplications", Application(chapter.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Db.PendingSubmissions.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Application_officer_still_enters_paper_application_while_switched_off() {
        var chapter = Builder.SeedChapter("C");
        SetOption(PublicFormOptions.MembershipApplication, null, "false");
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.ProcessApplications] });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/membershipapplications", Application(chapter.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Due_selection_refused_while_switched_off() {
        SetOption(PublicFormOptions.DueSelection, null, "false");
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/dueselector", DueSelection());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Motion_refused_for_chapter_without_public_form() {
        var chapter = Builder.SeedChapter("C");
        SetOption(PublicFormOptions.Motion, null, "false");
        using var client = await AnonymousWithCsrfAsync();

        var response = await client.PostAsJsonAsync("/api/motions", Motion(chapter.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.motion.public_form_disabled");
    }

    [Test]
    public async Task Motion_board_create_page_keeps_working_while_switched_off() {
        var chapter = Builder.SeedChapter("C");
        SetOption(PublicFormOptions.Motion, null, "false");
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.EditMotions] });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/motions", Motion(chapter.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Motion_switch_on_parent_applies_to_children_unless_overridden() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var otherLand = Builder.SeedChapter("Anderes Land", chain[0].Id);
        var archived = Builder.SeedChapter("Archiviert", chain[1].Id);
        Db.Chapters.Where(c => c.Id == archived.Id).Set(c => c.ArchivedAt, DateTime.UtcNow).Update();
        SetOption(PublicFormOptions.Motion, null, "false");
        SetOption(PublicFormOptions.Motion, chain[1].Id, "true");
        var kreisOff = Builder.SeedChapter("Kreis aus", chain[1].Id);
        SetOption(PublicFormOptions.Motion, kreisOff.Id, "false");
        using var client = AnonymousClient();

        var config = await client.GetFromJsonAsync<ClientConfigDTO>("/api/config/client");
        var motion = await (await AnonymousWithCsrfAsync()).PostAsJsonAsync("/api/motions", Motion(chain[2].Id));

        await Assert.That(config!.MotionFormChapterIds).IsEquivalentTo(new[] { chain[1].Id, chain[2].Id });
        await Assert.That(config.MotionFormChapterIds).DoesNotContain(otherLand.Id);
        await Assert.That(motion.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Client_config_reports_switched_off_forms() {
        Builder.SeedChapter("C");
        SetOption(PublicFormOptions.MembershipApplication, null, "false");
        SetOption(PublicFormOptions.DueSelection, null, "false");
        SetOption(PublicFormOptions.Motion, null, "false");
        using var client = AnonymousClient();

        var config = await client.GetFromJsonAsync<ClientConfigDTO>("/api/config/client");

        await Assert.That(config!.MembershipApplicationEnabled).IsFalse();
        await Assert.That(config.DueSelectionEnabled).IsFalse();
        await Assert.That(config.MotionFormChapterIds).IsEmpty();
    }
}
