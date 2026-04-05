using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.DueSelector;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.DueSelector;

public class DueSelectionCreateEndpointTests : IntegrationTestBase {
    private DueSelectionDTO ValidDto() {
        return new DueSelectionDTO {
            FirstName = "Alice",
            LastName = "Anderson",
            EMail = "alice@test.local",
            MemberNumber = 12345,
            SelectedValuation = SelectedValuation.OnePercentYearlyPay,
            YearlyIncome = 30000m,
            MonthlyIncomeGroup = 2500m,
            SelectedDue = 25m,
            ReducedAmount = 12m,
            ReducedJustification = "",
            ReducedTimeSpan = ReducedTimeSpan.OneYear,
            IsDirectDeposit = false,
            AccountHolder = "Alice Anderson",
            IBAN = "DE89370400440532013000",
            PaymentScedule = PaymentScedule.Annual
        };
    }

    [Test]
    public async Task Anonymous_can_post_due_selection() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/dueselector", ValidDto());
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Creates_persisted_due_selection() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/dueselector", ValidDto());
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.DueSelections.Count(d => d.FirstName == "Alice" && d.LastName == "Anderson");
        await Assert.That(persisted).IsEqualTo(1);
    }

    [Test]
    public async Task Persists_submitted_iban_and_account_holder() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var dto = ValidDto();
        dto.IBAN = "DE02700100800030876808";
        dto.AccountHolder = "Different Holder";
        var response = await client.PostAsJsonAsync("/api/dueselector", dto);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var saved = Db.DueSelections.First(d => d.FirstName == "Alice");
        await Assert.That(saved.IBAN).IsEqualTo("DE02700100800030876808");
        await Assert.That(saved.AccountHolder).IsEqualTo("Different Holder");
    }

    [Test]
    public async Task Persists_reduced_valuation_selection() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var dto = ValidDto();
        dto.SelectedValuation = SelectedValuation.Reduced;
        dto.ReducedAmount = 5m;
        dto.ReducedJustification = "Low income";
        var response = await client.PostAsJsonAsync("/api/dueselector", dto);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var saved = Db.DueSelections.First(d => d.FirstName == "Alice");
        await Assert.That(saved.ReducedAmount).IsEqualTo(5m);
        await Assert.That(saved.ReducedJustification).IsEqualTo("Low income");
    }
}
