using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.MembershipApplications;

public class ApplicationForwardingTests : IntegrationTestBase {
    private void SetOption(string identifier, string value) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(identifier, null, value);
    }

    private void SaveChapterSetting(ForwardingChapterSetting setting) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ForwardingSettingsRepository>().SaveChapter(setting, []);
    }

    private static MembershipApplicationDTO Dto(Guid? divisionId, Guid? chapterId = null, DueSelectionDTO? dues = null) => new() {
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
        ChapterId = chapterId,
        DueSelection = dues,
        ConformityDeclarationAccepted = true,
        EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    private async Task Submit(MembershipApplicationDTO dto) {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/membershipapplications", dto);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Client_chapter_is_ignored() {
        var federal = Builder.SeedChapter("Bund");
        var district = Builder.SeedChapter("Kreis", parentChapterId: federal.Id);
        var foreign = Builder.SeedChapter("Fremd", parentChapterId: federal.Id);
        var division = Builder.SeedDivisionFor(district.Id);

        await Submit(Dto(division, chapterId: foreign.Id));

        var application = Db.MembershipApplications.Single();
        await Assert.That(application.ChapterId).IsEqualTo(district.Id);
        await Assert.That(Db.Motions.Single().ChapterId).IsEqualTo(district.Id);
    }

    [Test]
    public async Task Receipt_mail_is_sent_on_redmine_path() {
        SetOption(ForwardingOptionKeys.MembershipApplicationEnabled, "true");
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        SaveChapterSetting(new ForwardingChapterSetting { ChapterId = state.Id, ApplicationMode = ForwardingMode.Redmine });
        var division = Builder.SeedDivisionFor(state.Id);

        await Submit(Dto(division));

        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
        await Assert.That(Db.ForwardingCases.Count()).IsEqualTo(1);
        await Assert.That(Db.NotificationLogs.Count(l => l.Recipient == "bea@test.local")).IsEqualTo(1);
    }

    [Test]
    public async Task Receipt_mail_on_redmine_path_is_cleared_with_the_case_data() {
        SetOption(ForwardingOptionKeys.MembershipApplicationEnabled, "true");
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        SaveChapterSetting(new ForwardingChapterSetting { ChapterId = state.Id, ApplicationMode = ForwardingMode.Redmine });
        var division = Builder.SeedDivisionFor(state.Id);
        await Submit(Dto(division));
        var forwardingCase = Db.ForwardingCases.Single();

        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ForwardingCaseRepository>().ClearPayload(forwardingCase.Id);

        var receipt = Db.NotificationLogs.Single(l => l.Recipient == "bea@test.local");
        await Assert.That(receipt.SourceEntityType).IsEqualTo("ForwardingCase");
        await Assert.That(receipt.SourceEntityId).IsEqualTo(forwardingCase.Id);
        await Assert.That(receipt.Body).IsNull();
    }

    [Test]
    public async Task Quartermaster_path_creates_motion_without_reduction() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        var division = Builder.SeedDivisionFor(state.Id);
        var dues = new DueSelectionDTO { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 36, ReducedJustification = "Studium" };

        await Submit(Dto(division, dues: dues));

        var application = Db.MembershipApplications.Single();
        var applicationMotion = Db.Motions.Single(m => m.LinkedMembershipApplicationId == application.Id);
        await Assert.That(applicationMotion.LinkedDueSelectionId).IsNull();
        await Assert.That(applicationMotion.Title).DoesNotContain("Beitragsminderung");
    }

    [Test]
    public async Task Reduction_gets_its_own_motion_in_deciding_chapter() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        var district = Builder.SeedChapter("Kreis", parentChapterId: state.Id);
        var division = Builder.SeedDivisionFor(district.Id);
        var dues = new DueSelectionDTO { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 36, ReducedJustification = "Studium" };

        await Submit(Dto(division, dues: dues));

        var application = Db.MembershipApplications.Single();
        var reductionMotion = Db.Motions.Single(m => m.LinkedDueSelectionId == application.DueSelectionId);
        await Assert.That(reductionMotion.ChapterId).IsEqualTo(state.Id);
        await Assert.That(Db.Motions.Single(m => m.LinkedMembershipApplicationId == application.Id).ChapterId).IsEqualTo(district.Id);
    }
}
