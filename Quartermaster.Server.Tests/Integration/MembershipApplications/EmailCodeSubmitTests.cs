using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Submissions;
using Quartermaster.Server.MembershipApplications.EmailCodes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.MembershipApplications;

public class EmailCodeSubmitTests : IntegrationTestBase {
    private const string CheckRoute = "/api/membershipapplications/email-code/check";
    private const string SubmitRoute = "/api/membershipapplications";

    private Guid _chapterId;

    private void SetUp() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        _chapterId = chapter.Id;
    }

    private MembershipApplicationDTO Dto(string email = "alice@test.local") {
        return new MembershipApplicationDTO {
            FirstName = "Alice",
            LastName = "Applicant",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Citizenship = "DE",
            Email = email,
            PhoneNumber = "0123456789",
            AddressStreet = "Teststr.",
            AddressHouseNbr = "1",
            AddressPostCode = "12345",
            AddressCity = "Testcity",
            AddressAdministrativeDivisionId = Builder.SeedDivisionFor(_chapterId),
            ConformityDeclarationAccepted = true,
            ApplicationText = "I want to join.",
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    private async Task<HttpResponseMessage> SubmitAnonymous(MembershipApplicationDTO dto) {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return await client.PostAsJsonAsync(SubmitRoute, dto);
    }

    private async Task<HttpResponseMessage> CheckAnonymous(string email, string code) {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return await client.PostAsJsonAsync(CheckRoute, new EmailCodeCheckRequest { Email = email, Code = code });
    }

    private static string OtherCode(string code) {
        return code == "000000" ? "000001" : "000000";
    }

    [Test]
    public async Task Check_endpoint_valid_and_wrong() {
        SetUp();
        var code = IssueEmailCode("alice@test.local");

        var wrong = await CheckAnonymous("alice@test.local", OtherCode(code));
        var valid = await CheckAnonymous("alice@test.local", code);

        await Assert.That(wrong.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await wrong.Content.ReadAsStringAsync()).Contains(I18nKey.Error.EmailCode.Wrong);
        await Assert.That(valid.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Anonymous_submit_without_code_refused() {
        SetUp();

        var response = await SubmitAnonymous(Dto());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.EmailCode.Required);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
        await Assert.That(Db.PendingSubmissions.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Anonymous_submit_with_wrong_code_refused() {
        SetUp();
        var dto = Dto();
        dto.EmailVerificationCode = OtherCode(IssueEmailCode(dto.Email));

        var response = await SubmitAnonymous(dto);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.EmailCode.Wrong);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
        await Assert.That(Db.EmailVerificationCodes.Single().FailedAttempts).IsEqualTo(1);
    }

    [Test]
    public async Task Anonymous_submit_with_code_for_other_address_refused() {
        SetUp();
        var dto = Dto("alice@test.local");
        dto.EmailVerificationCode = IssueEmailCode("bob@test.local");

        var response = await SubmitAnonymous(dto);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.EmailCode.Expired);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Anonymous_submit_with_code_stores_directly() {
        SetUp();
        var dto = Dto();
        dto.EmailVerificationCode = IssueEmailCode(dto.Email);

        var response = await SubmitAnonymous(dto);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var accepted = await response.Content.ReadFromJsonAsync<SubmissionAcceptedResponse>();
        await Assert.That(accepted!.RequiresConfirmation).IsFalse();
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(1);
        await Assert.That(Db.PendingSubmissions.Count()).IsEqualTo(0);
        await Assert.That(Db.EmailVerificationCodes.Count()).IsEqualTo(0);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == "application_received")).IsEqualTo(1);
    }

    [Test]
    public async Task Submit_after_check_uses_extended_lifetime() {
        SetUp();
        var dto = Dto();
        dto.EmailVerificationCode = IssueEmailCode(dto.Email);
        using (var scope = Factory.Services.CreateScope()) {
            var result = scope.ServiceProvider.GetRequiredService<EmailCodeService>()
                .Check(dto.Email, dto.EmailVerificationCode, DateTime.UtcNow.AddMinutes(14));
            await Assert.That(result).IsEqualTo(EmailCodeCheckResult.Valid);
        }
        await Assert.That(Db.EmailVerificationCodes.Single().ExpiresAt).IsGreaterThan(DateTime.UtcNow.AddMinutes(20));

        var response = await SubmitAnonymous(dto);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Code_survives_submit_refused_for_reduction() {
        SetUp();
        var dto = Dto();
        dto.EmailVerificationCode = IssueEmailCode(dto.Email);
        dto.DueSelection = new DueSelectionDTO {
            FirstName = "Alice",
            LastName = "Applicant",
            Email = dto.Email,
            SelectedValuation = SelectedValuation.Reduced,
            ReducedAmount = 100,
            ReducedJustification = "Studium",
            ReducedTimeSpan = ReducedTimeSpan.OneYear,
            PaymentSchedule = PaymentSchedule.Annual
        };

        var response = await SubmitAnonymous(dto);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.EmailVerificationCodes.Count()).IsEqualTo(1);
        using var scope = Factory.Services.CreateScope();
        var result = scope.ServiceProvider.GetRequiredService<EmailCodeService>().Check(dto.Email, dto.EmailVerificationCode, DateTime.UtcNow);
        await Assert.That(result).IsEqualTo(EmailCodeCheckResult.Valid);
    }

    [Test]
    public async Task Logged_in_submit_needs_no_code() {
        SetUp();
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync(SubmitRoute, Dto());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(1);
    }
}
