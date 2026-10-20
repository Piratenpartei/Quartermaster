using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.DueSelector;

public class DueSelectionChapterAccessTests : IntegrationTestBase {
    private DueSelection SeedReduction(Guid chapterId) {
        var selection = new DueSelection {
            FirstName = "Anna", LastName = "Pirat", Email = "anna@test.local", MemberNumber = 4711,
            SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 36, ReducedJustification = "Studium",
            Status = DueSelectionStatus.Pending, ChapterId = chapterId
        };
        Db.Insert(selection);
        return selection;
    }

    private string Officer(Guid chapterId, params string[] permissions)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapterId] = permissions }).Token;

    [Test]
    public async Task State_board_sees_its_reduction_in_list_and_detail() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        var selection = SeedReduction(state.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(Officer(state.Id, PermissionIdentifier.ViewDueSelections));

        var list = await client.GetAsync("/api/admin/dueselections?Page=1&PageSize=25");
        var detail = await client.GetAsync($"/api/admin/dueselections/{selection.Id}");

        await Assert.That(await list.Content.ReadAsStringAsync()).Contains(selection.Id.ToString());
        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task State_board_can_process_its_reduction() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        var selection = SeedReduction(state.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(Officer(state.Id, PermissionIdentifier.ProcessDueSelections));

        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new { Id = selection.Id, Status = DueSelectionStatus.Approved });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.DueSelections.Single(d => d.Id == selection.Id).Status).IsEqualTo(DueSelectionStatus.Approved);
    }

    [Test]
    public async Task Other_chapter_board_cannot_process_the_reduction() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        var other = Builder.SeedChapter("Anderes Land", parentChapterId: federal.Id);
        var selection = SeedReduction(state.Id);
        Builder.SeedMembershipApplication(other.Id);
        Db.MembershipApplications.Set(a => a.DueSelectionId, selection.Id).Update();
        using var client = await AuthenticatedClientWithCsrfAsync(Officer(other.Id, PermissionIdentifier.ProcessDueSelections, PermissionIdentifier.ViewDueSelections));

        var process = await client.PostAsJsonAsync("/api/admin/dueselections/process", new { Id = selection.Id, Status = DueSelectionStatus.Approved });
        var detail = await client.GetAsync($"/api/admin/dueselections/{selection.Id}");

        await Assert.That(process.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
    [Test]
    [Arguments(DueSelectionStatus.Approved)]
    [Arguments(DueSelectionStatus.AutoApproved)]
    [Arguments(DueSelectionStatus.Forwarded)]
    public async Task Decided_selection_cannot_be_processed_again(DueSelectionStatus decided) {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        var selection = SeedReduction(state.Id);
        Db.DueSelections.Where(d => d.Id == selection.Id).Set(d => d.Status, decided).Update();
        using var client = await AuthenticatedClientWithCsrfAsync(Officer(state.Id, PermissionIdentifier.ProcessDueSelections));

        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new { Id = selection.Id, Status = DueSelectionStatus.Rejected });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.admin.due_selection.already_decided");
        await Assert.That(Db.DueSelections.Single(d => d.Id == selection.Id).Status).IsEqualTo(decided);
    }
}
