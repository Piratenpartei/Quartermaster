using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Routing;

public class LegacyRouteRedirectTests : IntegrationTestBase {
    [Test]
    [Arguments("/Account/Benachrichtigungen", "/Account/Notifications")]
    [Arguments("/Administration/Akkreditierung", "/Administration/Accreditation")]
    public async Task German_route_redirects_permanently_and_keeps_the_query(string legacy, string current) {
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(legacy + "?tab=1");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.MovedPermanently);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo(current + "?tab=1");
    }
}
