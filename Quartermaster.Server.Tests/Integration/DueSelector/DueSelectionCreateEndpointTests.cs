using System.Linq;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using LinqToDB;
using System;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Options;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.DueSelector;

public class DueSelectionCreateEndpointTests : IntegrationTestBase {
    private void SetOption(string identifier, Guid? chapterId, string value) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(identifier, chapterId, value);
    }

    private static DueSelectionDTO ValidDto() {
        return new DueSelectionDTO {
            FirstName = "Alice",
            LastName = "Anderson",
            Email = "alice@test.local",
            MemberNumber = 12345,
            SelectedValuation = SelectedValuation.OnePercentYearlyPay,
            YearlyIncome = 30000m,
            MonthlyIncomeGroup = 2500m,
            SelectedDue = 300m,
            ReducedTimeSpan = ReducedTimeSpan.OneYear,
            IsDirectDeposit = true,
            AccountHolder = "Alice Anderson",
            IBAN = "DE89370400440532013000",
            PaymentSchedule = PaymentSchedule.Annual
        };
    }

    [Test]
    public async Task Anonymous_submit_is_401() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        var response = await client.PostAsJsonAsync("/api/dueselector", ValidDto());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(Db.DueSelections.Count()).IsEqualTo(0);
        await Assert.That(Db.PendingSubmissions.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Submission_needs_no_confirmation() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/dueselector", ValidDto());

        var accepted = await response.Content.ReadFromJsonAsync<SubmissionAcceptedResponse>();
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(accepted!.RequiresConfirmation).IsFalse();
        await Assert.That(Db.PendingSubmissions.Count()).IsEqualTo(0);
        await Assert.That(Db.DueSelections.Single().Status).IsEqualTo(DueSelectionStatus.AutoApproved);
    }

    [Test]
    public async Task Member_fields_are_taken_from_the_member() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        var district = Builder.SeedChapter("Kreis", parentChapterId: state.Id);
        var (user, token) = Builder.SeedAuthenticatedUser();
        Builder.SeedMember(chapterId: district.Id, memberNumber: 4711, firstName: "Echte", lastName: "Person", email: "echt@test.local", userId: user.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        await client.PostAsJsonAsync("/api/dueselector", ValidDto());

        var saved = Db.DueSelections.Single();
        await Assert.That(saved.MemberNumber).IsEqualTo(4711);
        await Assert.That(saved.FirstName).IsEqualTo("Echte");
        await Assert.That(saved.LastName).IsEqualTo("Person");
        await Assert.That(saved.Email).IsEqualTo("echt@test.local");
        await Assert.That(saved.ChapterId).IsEqualTo(state.Id);
        await Assert.That(saved.UserId).IsEqualTo(user.Id);
        await Assert.That(saved.IBAN).IsEqualTo("DE89370400440532013000");
    }

    [Test]
    public async Task Reduction_creates_motion_in_state_chapter() {
        var federal = Builder.SeedChapter("Bund");
        var state = Builder.SeedChapter("Land", parentChapterId: federal.Id);
        var (user, token) = Builder.SeedAuthenticatedUser();
        Builder.SeedMember(chapterId: state.Id, memberNumber: 4712, userId: user.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var dto = ValidDto();
        dto.SelectedValuation = SelectedValuation.Reduced;
        dto.ReducedAmount = 36m;
        dto.ReducedJustification = "Studium";

        await client.PostAsJsonAsync("/api/dueselector", dto);

        var saved = Db.DueSelections.Single();
        var motion = Db.Motions.Single(m => m.LinkedDueSelectionId == saved.Id);
        await Assert.That(saved.Status).IsEqualTo(DueSelectionStatus.Pending);
        await Assert.That(saved.ReducedAmount).IsEqualTo(36m);
        await Assert.That(motion.ChapterId).IsEqualTo(state.Id);
    }

    [Test]
    public async Task Due_selection_without_member_goes_to_federal_and_is_marked_by_hand() {
        SetOption(ForwardingOptionKeys.DueSelectionEnabled, null, "true");
        Builder.SeedChapter("Bund");
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/dueselector", ValidDto());

        var forwardingCase = Db.ForwardingCases.Single();
        var payload = JsonSerializer.Deserialize<ForwardingPayload>(forwardingCase.PayloadJson!)!;
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(forwardingCase.StateChapterId).IsNull();
        await Assert.That(payload.EnteredByHand).IsTrue();
        await Assert.That(payload.DueSelection!.MemberNumber).IsEqualTo(12345);
        await Assert.That(Db.ForwardingSteps.Single().Purpose).IsEqualTo(ForwardingPurpose.Regular);
        await Assert.That(Db.DueSelections.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Prefill_locks_for_linked_member() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Builder.SeedMember(memberNumber: 4713, firstName: "Echte", lastName: "Person", email: "echt@test.local", userId: user.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var prefill = await client.GetFromJsonAsync<DueSelectionPrefillDTO>("/api/dueselector/prefill");

        await Assert.That(prefill!.Locked).IsTrue();
        await Assert.That(prefill.MemberNumber).IsEqualTo(4713);
        await Assert.That(prefill.FirstName).IsEqualTo("Echte");
    }

    [Test]
    public async Task Prefill_without_member_is_editable() {
        var (_, token) = Builder.SeedAuthenticatedUser(firstName: "Konto", lastName: "Name");
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var prefill = await client.GetFromJsonAsync<DueSelectionPrefillDTO>("/api/dueselector/prefill");

        await Assert.That(prefill!.Locked).IsFalse();
        await Assert.That(prefill.MemberNumber).IsNull();
        await Assert.That(prefill.FirstName).IsEqualTo("Konto");
    }
    [Test]
    public async Task Floating_member_never_gets_the_client_number() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var member = Builder.SeedMember(firstName: "Neu", lastName: "Mitglied", userId: user.Id);
        Db.Members.Where(m => m.Id == member.Id).Set(m => m.MemberNumber, (int?)null).Update();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/dueselector", ValidDto());

        var saved = Db.DueSelections.Single();
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(saved.MemberNumber ?? 0).IsEqualTo(0);
        await Assert.That(saved.FirstName).IsEqualTo("Neu");
    }
}
