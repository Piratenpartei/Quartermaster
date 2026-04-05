using System.Threading.Tasks;
using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Tests.Rendering;

public class EdgeCaseMarkdownTests {
    [Test]
    public async Task German_umlauts_survive_rendering() {
        var result = MarkdownService.ToHtml("Grüße aus München. Straße und Schuß.");
        await Assert.That(result).Contains("Grüße");
        await Assert.That(result).Contains("München");
        await Assert.That(result).Contains("Straße");
        await Assert.That(result).Contains("Schuß");
    }

    [Test]
    public async Task Emoji_survives_rendering() {
        var result = MarkdownService.ToHtml("Hello 👋 world 🌍");
        await Assert.That(result).Contains("👋");
        await Assert.That(result).Contains("🌍");
    }

    [Test]
    public async Task RTL_text_survives_rendering() {
        var result = MarkdownService.ToHtml("Hebrew: שלום. Arabic: مرحبا");
        await Assert.That(result).Contains("שלום");
        await Assert.That(result).Contains("مرحبا");
    }

    [Test]
    public async Task Whitespace_only_input_returns_empty_or_whitespace() {
        var result = MarkdownService.ToHtml("   \n\t  ");
        // Should not throw, and result should be empty-ish
        await Assert.That(result.Trim()).IsEqualTo("");
    }

    [Test]
    public async Task Very_long_input_is_handled() {
        var longText = new string('a', 10000);
        var result = MarkdownService.ToHtml(longText);
        await Assert.That(result.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task HTML_entities_in_plain_text_are_escaped() {
        var result = MarkdownService.ToHtml("5 < 10 and 20 > 15 and tag = <foo>");
        await Assert.That(result).DoesNotContain("<foo>");
    }

    [Test]
    public async Task Javascript_url_in_link_stripped_in_Standard() {
        var result = MarkdownService.ToHtml("[click](javascript:alert(1))", SanitizationProfile.Standard);
        await Assert.That(result).DoesNotContain("javascript:");
        await Assert.That(result).DoesNotContain("alert");
    }

    [Test]
    public async Task Data_url_in_image_stripped() {
        // data: URLs in markdown images should be stripped by sanitizer
        var result = MarkdownService.ToHtml("![x](data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=)", SanitizationProfile.Standard);
        // Result should render without the scary data url; inline SVG is a known attack vector
        await Assert.That(result).DoesNotContain("svg+xml");
    }

    [Test]
    public async Task Event_handler_attributes_stripped() {
        // Try to sneak in onclick via raw HTML
        var result = MarkdownService.ToHtml("<div onclick=\"alert(1)\">click</div>");
        await Assert.That(result).DoesNotContain("onclick");
        await Assert.That(result).DoesNotContain("alert");
    }

    [Test]
    public async Task Nested_markdown_renders_correctly() {
        var result = MarkdownService.ToHtml("**bold with *italic inside***");
        await Assert.That(result).Contains("<strong>");
        await Assert.That(result).Contains("<em>");
    }

    [Test]
    public async Task Newlines_preserved_as_paragraphs() {
        var result = MarkdownService.ToHtml("First paragraph.\n\nSecond paragraph.");
        // Should render as two separate <p> elements
        var pCount = result.Split("<p").Length - 1;
        await Assert.That(pCount).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Windows_line_endings_handled() {
        var result = MarkdownService.ToHtml("Line 1\r\nLine 2\r\n");
        await Assert.That(result).Contains("Line 1");
        await Assert.That(result).Contains("Line 2");
    }
}
