using System.Threading.Tasks;
using Microsoft.Playwright;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.E2E;

public class MembershipApplicationE2ETests : E2ETestBase {
    [Test]
    public async Task Anonymous_user_can_reach_membership_application_form() {
        await Page.GotoAsync("/MembershipApplication/CountrySelection");
        // Wait for page content (h1..h5) past nav
        await Page.WaitForSelectorAsync("main h1, main h2, main h3, main h4, main h5, main .card, .card-body", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
        // Should not require login
        await Assert.That(Page.Url).DoesNotContain("/Login");
    }

    [Test]
    public async Task Personal_data_page_shows_inputs() {
        await Page.GotoAsync("/MembershipApplication/PersonalData");
        await Page.WaitForSelectorAsync("input", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
        var inputCount = await Page.Locator("input[type='text']").CountAsync();
        await Assert.That(inputCount).IsGreaterThan(0);
    }
}
