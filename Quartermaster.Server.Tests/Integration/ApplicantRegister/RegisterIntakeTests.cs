using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ApplicantRegister;

public class RegisterIntakeTests : IntegrationTestBase {
    private const string HitTriggerId = "applicant_register_hit";

    private static MembershipApplicationDTO Dto(Guid? divisionId, bool selfDeclared = false) => new() {
        FirstName = "Bea",
        LastName = "Neu",
        DateOfBirth = new DateOnly(1995, 3, 3),
        Citizenship = "DE",
        Email = "bea@test.local",
        PhoneNumber = "0123",
        AddressStreet = "Weg",
        AddressHouseNbr = "1",
        AddressPostCode = "30159",
        AddressCity = "Hannover",
        AddressAdministrativeDivisionId = divisionId,
        HasPriorDeclinedApplication = selfDeclared,
        ConformityDeclarationAccepted = true,
        EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    private async Task Submit(MembershipApplicationDTO dto) {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/membershipapplications", dto);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private ApplicantRegisterEntry SeedMatchingEntry(Guid chapterId) {
        var entry = Builder.SeedRegisterEntry(
            chapterId: chapterId,
            firstName: "Bea",
            lastName: "Neu",
            dateOfBirth: new DateTime(1995, 3, 3),
            street: "Anderswo",
            postCode: "99999",
            city: "Woanders",
            email: "bea@test.local",
            occurredOn: new DateTime(2024, 6, 7),
            note: "Geheimnotiz");
        return entry;
    }

    private (Guid Root, Guid State) SeedQuartermasterPath() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        SageSetupSeeder.Seed(Factory.Services, federal.Id, state.Id);
        Builder.SeedAuthenticatedUser(
            email: "holder@test.local",
            globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister });
        return (federal.Id, state.Id);
    }

    [Test]
    public async Task Hit_requires_federal_approval_and_records_hit() {
        var (root, state) = SeedQuartermasterPath();
        var entry = SeedMatchingEntry(root);

        await Submit(Dto(Builder.SeedDivisionFor(state)));

        var application = Db.MembershipApplications.Single();
        await Assert.That(application.RequiresFederalApproval).IsTrue();
        var hit = Db.ApplicationRegisterHits.Single();
        await Assert.That(hit.ApplicationId).IsEqualTo(application.Id);
        await Assert.That(hit.EntryId).IsEqualTo(entry.Id);
    }

    [Test]
    public async Task Self_declaration_requires_federal_approval_without_hit() {
        var (_, state) = SeedQuartermasterPath();

        await Submit(Dto(Builder.SeedDivisionFor(state), selfDeclared: true));

        await Assert.That(Db.MembershipApplications.Single().RequiresFederalApproval).IsTrue();
        await Assert.That(Db.ApplicationRegisterHits.Count()).IsEqualTo(0);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == HitTriggerId)).IsEqualTo(0);
    }

    [Test]
    public async Task No_hit_leaves_application_normal() {
        var (root, state) = SeedQuartermasterPath();
        Builder.SeedRegisterEntry(chapterId: root, firstName: "Zed", lastName: "Other", email: "zed@test.local");

        await Submit(Dto(Builder.SeedDivisionFor(state)));

        await Assert.That(Db.MembershipApplications.Single().RequiresFederalApproval).IsFalse();
        await Assert.That(Db.ApplicationRegisterHits.Count()).IsEqualTo(0);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == HitTriggerId)).IsEqualTo(0);
    }

    [Test]
    public async Task Chapter_motion_names_no_entry() {
        var (root, state) = SeedQuartermasterPath();
        SeedMatchingEntry(root);

        await Submit(Dto(Builder.SeedDivisionFor(state)));

        var text = Db.Motions.Single().TextMarkdown;
        await Assert.That(text).Contains("Benötigt zusätzlich die Zustimmung des Bundesvorstands (§ 3 Abs. 2 BS).");
        await Assert.That(text).DoesNotContain("Aufnahme abgelehnt");
        await Assert.That(text).DoesNotContain("07.06.2024");
        await Assert.That(text).DoesNotContain("Geheimnotiz");
    }

    [Test]
    public async Task Hit_notification_links_application() {
        var (root, state) = SeedQuartermasterPath();
        SeedMatchingEntry(root);

        await Submit(Dto(Builder.SeedDivisionFor(state)));

        var application = Db.MembershipApplications.Single();
        var log = Db.NotificationLogs.Single(l => l.TriggerId == HitTriggerId);
        await Assert.That(log.SourceEntityType).IsEqualTo("MembershipApplication");
        await Assert.That(log.SourceEntityId).IsEqualTo(application.Id);
        await Assert.That(log.Body!).Contains($"/Administration/MembershipApplications/{application.Id}");
        await Assert.That(log.Body!).Contains("einem Eintrag");
    }

    /// <summary>Mail and Telegram are unencrypted, so neither the applicant nor the register entry is named.</summary>
    [Test]
    public async Task Hit_notification_names_nobody() {
        var (root, state) = SeedQuartermasterPath();
        SeedMatchingEntry(root);

        await Submit(Dto(Builder.SeedDivisionFor(state)));

        var log = Db.NotificationLogs.Single(l => l.TriggerId == HitTriggerId);
        foreach (var text in new[] { log.Subject, log.Body! }) {
            await Assert.That(text).DoesNotContain("Bea");
            await Assert.That(text).DoesNotContain("Neu");
            await Assert.That(text).DoesNotContain("Hannover");
            await Assert.That(text).DoesNotContain("07.06.2024");
        }
    }

    [Test]
    public async Task Hit_notification_links_entry_while_waiting_for_division() {
        var (root, _) = SeedQuartermasterPath();
        var entry = SeedMatchingEntry(root);

        await Submit(Dto(null));

        var application = Db.MembershipApplications.Single();
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.PendingDivisionLinking);
        var log = Db.NotificationLogs.Single(l => l.TriggerId == HitTriggerId);
        await Assert.That(log.Body!).Contains($"/Administration/ApplicantRegister/{entry.Id}");
        await Assert.That(log.Body!).DoesNotContain("/Administration/MembershipApplications/");
    }

    [Test]
    public async Task Hit_notification_links_entry_without_application() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        SageSetupSeeder.Seed(Factory.Services, federal.Id);
        using (var scope = Factory.Services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<ForwardingSettingsRepository>()
                .SaveChapter(new ForwardingChapterSetting { ChapterId = state.Id, ApplicationMode = ForwardingMode.Redmine }, []);
        }

        Builder.SeedAuthenticatedUser(
            email: "holder@test.local",
            globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister });
        var entry = SeedMatchingEntry(federal.Id);

        await Submit(Dto(Builder.SeedDivisionFor(state.Id)));

        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
        var log = Db.NotificationLogs.Single(l => l.TriggerId == HitTriggerId);
        await Assert.That(log.Body!).Contains($"/Administration/ApplicantRegister/{entry.Id}");
        var forwardingCase = Db.ForwardingCases.Single();
        await Assert.That(forwardingCase.PayloadJson!).Contains("Zustimmung des Bundesvorstands erforderlich");
    }

    [Test]
    public async Task Waiting_application_notice_survives_linking_to_redmine() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        SageSetupSeeder.Seed(Factory.Services, federal.Id);
        using (var scope = Factory.Services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<ForwardingSettingsRepository>()
                .SaveChapter(new ForwardingChapterSetting { ChapterId = state.Id, ApplicationMode = ForwardingMode.Redmine }, []);
        }

        SeedMatchingEntry(federal.Id);
        await Submit(Dto(null));
        var application = Db.MembershipApplications.Single();
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.PendingDivisionLinking);
        await Assert.That(Db.ApplicationRegisterHits.Count()).IsEqualTo(1);

        var division = Builder.SeedDivisionFor(state.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.LinkApplicationDivision });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/link-division",
            new { Id = application.Id, AdministrativeDivisionId = division, NotInGermany = false });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
        await Assert.That(Db.ApplicationRegisterHits.Count()).IsEqualTo(0);
        var forwardingCase = Db.ForwardingCases.Single();
        var payload = JsonSerializer.Deserialize<ForwardingPayload>(forwardingCase.PayloadJson!)!;
        await Assert.That(payload.RegisterNotice).Contains("Zustimmung des Bundesvorstands erforderlich");
    }
}
