using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Tests.Rendering;

public class HtmlSanitizationServiceStrictTests {
    [Test]
    public async Task RemovesScriptTags_KeepsText() {
        var result = HtmlSanitizationService.Sanitize(
            "<p>Hello</p><script>alert('xss')</script>", SanitizationProfile.Strict);

        await Assert.That(result).Contains("Hello");
        await Assert.That(result).DoesNotContain("<script");
        await Assert.That(result).DoesNotContain("alert");
    }

    [Test]
    public async Task RemovesOnclickHandlers() {
        var result = HtmlSanitizationService.Sanitize(
            "<p onclick=\"alert('xss')\">Click me</p>", SanitizationProfile.Strict);

        await Assert.That(result).Contains("Click me");
        await Assert.That(result).DoesNotContain("onclick");
        await Assert.That(result).DoesNotContain("alert");
    }

    [Test]
    public async Task StripsLinkHref_KeepsText() {
        var result = HtmlSanitizationService.Sanitize(
            "<p>Visit <a href=\"https://example.com\">here</a> for info</p>",
            SanitizationProfile.Strict);

        await Assert.That(result).DoesNotContain("href");
        await Assert.That(result).Contains("here");
        await Assert.That(result).Contains("Visit");
    }

    [Test]
    public async Task RemovesTables() {
        var result = HtmlSanitizationService.Sanitize(
            "<table><tr><td>Cell</td></tr></table>", SanitizationProfile.Strict);

        await Assert.That(result).DoesNotContain("<table");
        await Assert.That(result).DoesNotContain("<td");
    }

    [Test]
    public async Task RemovesImages() {
        var result = HtmlSanitizationService.Sanitize(
            "<img src=\"https://evil.com/img.jpg\" alt=\"pic\">", SanitizationProfile.Strict);

        await Assert.That(result).DoesNotContain("<img");
        await Assert.That(result).DoesNotContain("src");
    }

    [Test]
    public async Task AllowsFormattingTags() {
        var result = HtmlSanitizationService.Sanitize(
            "<strong>Bold</strong> <em>Italic</em> <code>Code</code>", SanitizationProfile.Strict);

        await Assert.That(result).Contains("<strong>Bold</strong>");
        await Assert.That(result).Contains("<em>Italic</em>");
        await Assert.That(result).Contains("<code>Code</code>");
    }

    [Test]
    public async Task AllowsHeadings() {
        var result = HtmlSanitizationService.Sanitize(
            "<h1>Title</h1><h3>Subtitle</h3>", SanitizationProfile.Strict);

        await Assert.That(result).Contains("<h1>Title</h1>");
        await Assert.That(result).Contains("<h3>Subtitle</h3>");
    }

    [Test]
    public async Task AllowsLists() {
        var result = HtmlSanitizationService.Sanitize(
            "<ul><li>Item 1</li><li>Item 2</li></ul>", SanitizationProfile.Strict);

        await Assert.That(result).Contains("<ul>");
        await Assert.That(result).Contains("<li>Item 1</li>");
        await Assert.That(result).Contains("<li>Item 2</li>");
    }

    [Test]
    public async Task RemovesStyleTags() {
        var result = HtmlSanitizationService.Sanitize(
            "<style>body { display: none; }</style><p>Text</p>", SanitizationProfile.Strict);

        await Assert.That(result).Contains("Text");
        await Assert.That(result).DoesNotContain("<style");
        await Assert.That(result).DoesNotContain("display");
    }

    [Test]
    public async Task RemovesIframes() {
        var result = HtmlSanitizationService.Sanitize(
            "<iframe src=\"https://evil.com\"></iframe><p>Safe</p>", SanitizationProfile.Strict);

        await Assert.That(result).Contains("Safe");
        await Assert.That(result).DoesNotContain("<iframe");
    }
}

public class HtmlSanitizationServiceStandardTests {
    [Test]
    public async Task RemovesScriptTags() {
        var result = HtmlSanitizationService.Sanitize(
            "<p>Hello</p><script>alert('xss')</script>", SanitizationProfile.Standard);

        await Assert.That(result).Contains("Hello");
        await Assert.That(result).DoesNotContain("<script");
        await Assert.That(result).DoesNotContain("alert");
    }

    [Test]
    public async Task RemovesOnclickHandlers() {
        var result = HtmlSanitizationService.Sanitize(
            "<a href=\"https://ok.com\" onclick=\"alert('xss')\">Link</a>", SanitizationProfile.Standard);

        await Assert.That(result).Contains("Link");
        await Assert.That(result).DoesNotContain("onclick");
        await Assert.That(result).DoesNotContain("alert");
    }

    [Test]
    public async Task AllowsLinks() {
        var result = HtmlSanitizationService.Sanitize(
            "<a href=\"https://example.com\">Link text</a>", SanitizationProfile.Standard);

        await Assert.That(result).Contains("<a");
        await Assert.That(result).Contains("href");
        await Assert.That(result).Contains("Link text");
    }

    [Test]
    public async Task AllowsTables() {
        var result = HtmlSanitizationService.Sanitize(
            "<table><thead><tr><th>Header</th></tr></thead><tbody><tr><td>Cell</td></tr></tbody></table>",
            SanitizationProfile.Standard);

        await Assert.That(result).Contains("<table>");
        await Assert.That(result).Contains("<thead>");
        await Assert.That(result).Contains("<th>Header</th>");
        await Assert.That(result).Contains("<td>Cell</td>");
    }

    [Test]
    public async Task AllowsImages() {
        var result = HtmlSanitizationService.Sanitize(
            "<img src=\"https://example.com/img.jpg\" alt=\"A picture\">", SanitizationProfile.Standard);

        await Assert.That(result).Contains("<img");
        await Assert.That(result).Contains("src");
        await Assert.That(result).Contains("alt");
    }

    [Test]
    public async Task BlocksJavascriptUrls() {
        var result = HtmlSanitizationService.Sanitize(
            "<a href=\"javascript:alert('xss')\">Click</a>", SanitizationProfile.Standard);

        await Assert.That(result).DoesNotContain("javascript");
    }

    [Test]
    public async Task RemovesIframes() {
        var result = HtmlSanitizationService.Sanitize(
            "<iframe src=\"https://evil.com\"></iframe><p>Safe</p>", SanitizationProfile.Standard);

        await Assert.That(result).Contains("Safe");
        await Assert.That(result).DoesNotContain("<iframe");
    }

    [Test]
    public async Task AllowsFormattingTags() {
        var result = HtmlSanitizationService.Sanitize(
            "<strong>Bold</strong> <em>Italic</em> <code>Code</code>", SanitizationProfile.Standard);

        await Assert.That(result).Contains("<strong>Bold</strong>");
        await Assert.That(result).Contains("<em>Italic</em>");
        await Assert.That(result).Contains("<code>Code</code>");
    }
}
