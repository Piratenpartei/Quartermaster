using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Tests.Rendering;

public class MarkdownServiceTests {
    [Test]
    public async Task ConvertsBasicMarkdown_BoldAndItalic() {
        var result = MarkdownService.ToHtml("**bold** and *italic*");

        await Assert.That(result).Contains("<strong>");
        await Assert.That(result).Contains("<em>");
    }

    [Test]
    public async Task Strict_RemovesLinks() {
        var result = MarkdownService.ToHtml("[click here](https://example.com)", SanitizationProfile.Strict);

        await Assert.That(result).Contains("click here");
        await Assert.That(result).DoesNotContain("href");
    }

    [Test]
    public async Task Standard_KeepsLinks() {
        var result = MarkdownService.ToHtml("[click here](https://example.com)", SanitizationProfile.Standard);

        await Assert.That(result).Contains("<a");
        await Assert.That(result).Contains("href");
    }

    [Test]
    public async Task Strict_RemovesTables() {
        var markdown = "| Col1 | Col2 |\n| --- | --- |\n| A | B |";
        var result = MarkdownService.ToHtml(markdown, SanitizationProfile.Strict);

        await Assert.That(result).DoesNotContain("<table");
    }

    [Test]
    public async Task Standard_KeepsTables() {
        var markdown = "| Col1 | Col2 |\n| --- | --- |\n| A | B |";
        var result = MarkdownService.ToHtml(markdown, SanitizationProfile.Standard);

        await Assert.That(result).Contains("<table");
        await Assert.That(result).Contains("<td>");
    }

    [Test]
    public async Task StripsScriptInjection() {
        var result = MarkdownService.ToHtml("Hello <script>alert('xss')</script> world");

        await Assert.That(result).Contains("Hello");
        await Assert.That(result).DoesNotContain("<script");
        await Assert.That(result).DoesNotContain("alert");
    }

    [Test]
    public async Task EmptyInput_ReturnsEmptyString() {
        await Assert.That(MarkdownService.ToHtml("")).IsEqualTo("");
        await Assert.That(MarkdownService.ToHtml(null!)).IsEqualTo("");
    }

    [Test]
    public async Task DefaultProfile_IsStandard_LinksWork() {
        var result = MarkdownService.ToHtml("[link](https://example.com)");

        await Assert.That(result).Contains("<a");
        await Assert.That(result).Contains("href");
    }

    [Test]
    public async Task Strict_RemovesImages() {
        var result = MarkdownService.ToHtml("![alt text](https://example.com/img.jpg)", SanitizationProfile.Strict);

        await Assert.That(result).DoesNotContain("<img");
    }

    [Test]
    public async Task Standard_KeepsImages() {
        var result = MarkdownService.ToHtml("![alt text](https://example.com/img.jpg)", SanitizationProfile.Standard);

        await Assert.That(result).Contains("<img");
    }
}
