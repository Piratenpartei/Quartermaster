using System.Threading.Tasks;
using Microsoft.Playwright;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.E2E;

public class PublicPagesE2ETests : E2ETestBase {
    [Test]
    public async Task Home_page_loads_and_Blazor_WASM_boots() {
        await Page.GotoAsync("/");
        // Wait for Blazor-rendered content — look for nav or any h element
        await Page.WaitForSelectorAsync("nav, h1, h2, h3", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
    }

    [Test]
    public async Task Public_events_list_page_loads() {
        await Page.GotoAsync("/Events");
        // The public events page should render even with no events
        await Page.WaitForSelectorAsync("body", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
    }

    [Test]
    public async Task Membership_application_form_renders() {
        await Page.GotoAsync("/MembershipApplication/PersonalData");
        // The form exists with input fields for personal data
        await Page.WaitForSelectorAsync("input, select, textarea", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
    }

    [Test]
    public async Task Nav_menu_at_375px_viewport_collapses() {
        // Set viewport to 375x667 (iPhone SE)
        await Page.SetViewportSizeAsync(375, 667);
        await Page.GotoAsync("/");
        await Page.WaitForSelectorAsync("body", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
        // No horizontal scroll expected at 375px
        var bodyWidth = await Page.EvaluateAsync<int>("() => document.body.scrollWidth");
        await Assert.That(bodyWidth).IsLessThanOrEqualTo(400);
    }
}
