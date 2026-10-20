using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Security;

public class DueSelectionRateLimitTests : IntegrationTestBase {
    private static DueSelectionDTO Dto() => new() {
        FirstName = "Anna",
        LastName = "Pirat",
        Email = "anna@test.local",
        MemberNumber = 4711,
        SelectedValuation = SelectedValuation.MonthlyPayGroup,
        MonthlyIncomeGroup = 2000,
        SelectedDue = 120,
        PaymentSchedule = PaymentSchedule.Annual
    };

    private static async Task<HttpClient> ClientFor(WebApplicationFactory<Program> factory, string token) {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/antiforgery/token");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", payload.GetProperty("token").GetString());
        return client;
    }

    [Test]
    public async Task Third_submission_of_one_account_in_the_window_returns_429() {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue("auth.ratelimit.due_selection_permits", null, "2");
        var (_, first) = Builder.SeedAuthenticatedUser();
        var (_, second) = Builder.SeedAuthenticatedUser();

        // A fresh factory builds a new rate limiter, which reads the strict value when it creates the partitions.
        using var strict = Factory.WithWebHostBuilder(_ => { });
        using var spammer = await ClientFor(strict, first);
        using var other = await ClientFor(strict, second);

        var one = await spammer.PostAsJsonAsync("/api/dueselector", Dto());
        var two = await spammer.PostAsJsonAsync("/api/dueselector", Dto());
        var three = await spammer.PostAsJsonAsync("/api/dueselector", Dto());
        var otherAccount = await other.PostAsJsonAsync("/api/dueselector", Dto());

        await Assert.That(one.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(two.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((int)three.StatusCode).IsEqualTo(429);
        await Assert.That(otherAccount.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
