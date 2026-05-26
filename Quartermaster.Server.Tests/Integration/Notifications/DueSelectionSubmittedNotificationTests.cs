using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Notifications;

public class DueSelectionSubmittedNotificationTests : IntegrationTestBase {
    private DueSelectionDTO Dto(int memberNumber, string first = "Max", string last = "Mustermann") =>
        new() {
            FirstName = first,
            LastName = last,
            Email = "submitter@test.local",
            MemberNumber = memberNumber,
            SelectedValuation = SelectedValuation.Reduced,
            YearlyIncome = 0,
            MonthlyIncomeGroup = 0,
            ReducedAmount = 12m,
            SelectedDue = 12m,
            ReducedJustification = "Studierend ohne Einkommen",
            ReducedTimeSpan = ReducedTimeSpan.OneYear,
            IsDirectDeposit = false,
            AccountHolder = "Max Mustermann",
            IBAN = "DE89370400440532013000",
            PaymentSchedule = PaymentSchedule.Annual
        };

    private async Task<HttpResponseMessage> Submit(DueSelectionDTO dto) {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return await client.PostAsJsonAsync("/api/dueselector", dto);
    }

    [Test]
    public async Task Notifies_ProcessDueSelections_holders_when_member_resolves_to_chapter() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapterId: chapter.Id, memberNumber: 42_001);
        var (intake, _) = Builder.SeedAuthenticatedUser(
            email: "intake@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ProcessDueSelections } });

        var response = await Submit(Dto(memberNumber: member.MemberNumber));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "due_selection_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].RecipientUserId).IsEqualTo(intake.Id);
        await Assert.That(logs[0].SourceEntityType).IsEqualTo("DueSelection");
    }

    [Test]
    public async Task Skips_dispatch_when_member_number_unknown() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedAuthenticatedUser(
            email: "intake@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ProcessDueSelections } });

        var response = await Submit(Dto(memberNumber: 999_999));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "due_selection_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Skips_dispatch_when_member_has_no_chapter() {
        Builder.SeedAuthenticatedUser(
            email: "global@test.local",
            globalPermissions: new[] { PermissionIdentifier.ProcessDueSelections });
        var member = Builder.SeedMember(chapterId: null, memberNumber: 42_002);

        await Submit(Dto(memberNumber: member.MemberNumber));

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "due_selection_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(0);
    }
}
