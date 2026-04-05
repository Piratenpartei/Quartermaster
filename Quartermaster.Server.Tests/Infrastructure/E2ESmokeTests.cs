using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Quartermaster.Server.Tests.Infrastructure;

public class E2ESmokeTests : E2ETestBase {
    [Test]
    public async Task Home_page_loads() {
        await Page.GotoAsync("/");
        // Blazor WASM takes a moment to boot. Wait for the app root to be populated.
        await Page.WaitForSelectorAsync("h1, h2, h3, h4, nav", new PageWaitForSelectorOptions {
            Timeout = 30000
        });
    }

    [Test]
    public async Task Login_page_is_reachable() {
        // First, try fetching the root — should return index.html via MapFallbackToFile
        var rootResp = await Page.GotoAsync("/");
        if (rootResp == null || !rootResp.Ok) {
            var body = rootResp != null ? await rootResp.TextAsync() : "(null response)";
            throw new Exception($"/ returned {rootResp?.Status}: {body[..Math.Min(500, body.Length)]}");
        }
        // Then /Login (should be mapped via fallback to index.html too)
        var response = await Page.GotoAsync("/Login");
        await Assert.That(response).IsNotNull();
        if (!response!.Ok) {
            var body = await response.TextAsync();
            throw new Exception($"/Login returned status {response.Status}: {body[..Math.Min(500, body.Length)]}");
        }
    }
}
