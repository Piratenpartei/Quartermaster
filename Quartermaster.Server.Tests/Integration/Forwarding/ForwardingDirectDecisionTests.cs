using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Forwarding;

public class ForwardingDirectDecisionTests : IntegrationTestBase {
    private ForwardingStep SeedWaitingNote(ForwardingCaseKind kind, Guid? applicationId, Guid? dueSelectionId, Guid federalId) {
        var forwardingCase = new ForwardingCase {
            Kind = kind,
            MembershipApplicationId = applicationId,
            DueSelectionId = dueSelectionId,
            CreatedAt = DateTime.UtcNow
        };
        var note = new ForwardingStep {
            Order = 2, Kind = ForwardingStepKind.AddOutcomeNote, TargetChapterId = federalId, Purpose = ForwardingPurpose.Reduction,
            State = ForwardingStepState.Waiting, UpdatedAt = DateTime.UtcNow
        };
        new ForwardingCaseRepository(Db).Create(forwardingCase, [note]);
        return note;
    }

    [Test]
    public async Task Processing_a_due_selection_directly_releases_the_note() {
        var federal = Builder.SeedChapter("Bund");
        var selection = new DueSelection { FirstName = "Anna", LastName = "Pirat", SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 36, Status = DueSelectionStatus.Pending };
        Db.Insert(selection);
        var note = SeedWaitingNote(ForwardingCaseKind.DueSelection, null, selection.Id, federal.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ProcessDueSelections });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new { Id = selection.Id, Status = DueSelectionStatus.Approved });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.ForwardingSteps.Single(s => s.Id == note.Id).State).IsEqualTo(ForwardingStepState.Pending);
    }

    [Test]
    public async Task Processing_an_application_directly_releases_the_note() {
        var federal = Builder.SeedChapter("Bund");
        var application = Builder.SeedMembershipApplication(federal.Id);
        var note = SeedWaitingNote(ForwardingCaseKind.Application, application.Id, null, federal.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new { Id = application.Id, Status = ApplicationStatus.Approved });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.ForwardingSteps.Single(s => s.Id == note.Id).State).IsEqualTo(ForwardingStepState.Pending);
    }
}
