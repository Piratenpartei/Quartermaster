using System.Threading.Tasks;
using Microsoft.Playwright;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.E2E;

public class LoginFlowE2ETests : E2ETestBase {
    [Test]
    public async Task Login_page_loads_with_SSO_and_manual_cards() {
        await Page.GotoAsync("/Login");
        // Wait for Blazor to render the page content
        await Page.WaitForSelectorAsync("text=Anmelden", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
        // Manual login link should be visible
        var manualLink = Page.Locator("a[href='/Login/Manual']");
        await Assert.That(await manualLink.CountAsync()).IsGreaterThan(0);
    }

    [Test]
    public async Task Manual_login_form_renders_all_fields() {
        await Page.GotoAsync("/Login/Manual");
        await Page.WaitForSelectorAsync("input[autocomplete='username']", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
        await Assert.That(await Page.Locator("input[autocomplete='username']").CountAsync()).IsEqualTo(1);
        await Assert.That(await Page.Locator("input[autocomplete='current-password']").CountAsync()).IsEqualTo(1);
        await Assert.That(await Page.Locator("button[type='submit']").CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task Manual_login_form_accepts_input() {
        await Page.GotoAsync("/Login/Manual");
        await Page.WaitForSelectorAsync("input[autocomplete='username']", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
        await Page.FillAsync("input[autocomplete='username']", "someuser");
        await Page.FillAsync("input[autocomplete='current-password']", "somepassword");
        var filledUsername = await Page.InputValueAsync("input[autocomplete='username']");
        await Assert.That(filledUsername).IsEqualTo("someuser");
    }
}
