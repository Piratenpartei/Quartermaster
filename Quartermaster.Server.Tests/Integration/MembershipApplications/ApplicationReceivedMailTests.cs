using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.MembershipApplications;

public class ApplicationReceivedMailTests : IntegrationTestBase {
    private MembershipApplicationDTO Dto(Guid? chapterId) =>
        new() {
            FirstName = "Alice",
            LastName = "Applicant",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Citizenship = "DE",
            Email = "applicant@test.local",
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

    private async Task Submit(MembershipApplicationDTO dto) {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        dto.EmailVerificationCode = IssueEmailCode(dto.Email);
        await client.PostAsJsonAsync("/api/membershipapplications", dto);
    }

    [Test]
    public async Task No_received_mail_without_code() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        await client.PostAsJsonAsync("/api/membershipapplications", Dto(chapter.Id));

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_received").ToList();
        await Assert.That(logs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Sends_received_mail_to_applicant_on_submit() {
        var chapter = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, chapter.Id, chapter.Id);
        await Submit(Dto(chapter.Id));

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_received").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("applicant@test.local");
        await Assert.That(logs[0].SourceEntityType).IsEqualTo("MembershipApplication");
    }

    [Test]
    public async Task Sends_received_mail_even_without_chapter() {
        var root = Builder.SeedChapter("C");
        SageSetupSeeder.Seed(Factory.Services, root.Id);
        await Submit(Dto(chapterId: null));

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_received").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("applicant@test.local");
    }
}
