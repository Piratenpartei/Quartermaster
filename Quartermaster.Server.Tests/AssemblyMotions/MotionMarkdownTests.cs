using Markdig;
using Quartermaster.Rendering;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class MotionMarkdownTests {
    [Test]
    public async Task Html_block_is_a_problem() {
        var problems = MotionMarkdown.FindProblems("<div>\nSome content\n</div>");
        await Assert.That(problems).IsEquivalentTo([MotionMarkdownProblem.RawHtml]);
    }

    [Test]
    public async Task Inline_html_is_a_problem() {
        var problems = MotionMarkdown.FindProblems("Text <b>x</b>");
        await Assert.That(problems).IsEquivalentTo([MotionMarkdownProblem.RawHtml]);
    }

    [Test]
    public async Task Image_is_a_problem() {
        var problems = MotionMarkdown.FindProblems("![alt](image.png)");
        await Assert.That(problems).IsEquivalentTo([MotionMarkdownProblem.Image]);
    }

    [Test]
    public async Task Table_and_strikethrough_are_fine() {
        var markdown = "| a | b |\n| - | - |\n| 1 | 2 |\n\n~~gone~~\n";
        var problems = MotionMarkdown.FindProblems(markdown);
        await Assert.That(problems).IsEmpty();
    }

    [Test]
    public async Task Plain_motion_text_has_no_problems() {
        var markdown = "# Heading\n\n"
            + "Some **bold** text with a [link](https://example.com) and `inline code`.\n\n"
            + "> A quote\n\n"
            + "- item one\n- item two\n\n"
            + "```\ncode block\n```\n\n"
            + "---\n";
        var problems = MotionMarkdown.FindProblems(markdown);
        await Assert.That(problems).IsEmpty();
    }

    [Test]
    public async Task Motion_flavour_renders_footnote_syntax_as_text() {
        var html = MarkdownService.ToHtml("Text[^1]", SanitizationProfile.Standard, MarkdownFlavor.Motion);
        await Assert.That(html).Contains("[^1]");
        await Assert.That(html).DoesNotContain("footnote");
    }

    [Test]
    public async Task Full_flavour_unchanged() {
        var markdown = "| a | b |\n| - | - |\n| 1 | 2 |\n\nText[^1]\n\n[^1]: a note\n";

        var fullPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        var expectedRaw = Markdown.ToHtml(markdown, fullPipeline);
        var expected = HtmlSanitizationService.Sanitize(expectedRaw, SanitizationProfile.Standard);

        var actual = MarkdownService.ToHtml(markdown);

        await Assert.That(actual).IsEqualTo(expected);
    }
}
