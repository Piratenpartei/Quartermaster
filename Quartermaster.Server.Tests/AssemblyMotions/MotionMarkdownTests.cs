using System;
using System.Linq;
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

    [Test]
    [Arguments("> - ", 60)]
    [Arguments(">", 500)]
    [Arguments("[a", 200)]
    public async Task Excessive_nesting_is_reported_not_thrown(string unit, int repeat) {
        var markdown = string.Concat(Enumerable.Repeat(unit, repeat)) + "x";
        var problems = MotionMarkdown.FindProblems(markdown);
        await Assert.That(problems).IsEquivalentTo([MotionMarkdownProblem.TooDeeplyNested]);
        await Assert.That(MarkdownService.ToHtml(markdown, SanitizationProfile.Standard, MarkdownFlavor.Motion)).IsEqualTo("");
    }

    [Test]
    public async Task Markdig_signals_nesting_with_argument_exception() {
        var markdown = string.Concat(Enumerable.Repeat("[a", 500));
        await Assert.That(() => Markdig.Markdown.ToHtml(markdown, MotionMarkdown.Pipeline)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Over_the_length_limit_is_reported_and_nothing_else() {
        var markdown = new string('a', MotionMarkdown.MaxLength + 1);
        await Assert.That(MotionMarkdown.FindProblems(markdown)).IsEquivalentTo([MotionMarkdownProblem.TooLong]);
        await Assert.That(MarkdownService.ToHtml(markdown, SanitizationProfile.Standard, MarkdownFlavor.Motion)).IsEqualTo("");
        await Assert.That(MotionMarkdown.FindProblems(new string('a', MotionMarkdown.MaxLength))).IsEmpty();
    }

    [Test]
    public async Task Full_flavour_is_not_limited_by_the_motion_rules() {
        var html = MarkdownService.ToHtml(new string('a', MotionMarkdown.MaxLength + 1), SanitizationProfile.Standard, MarkdownFlavor.Full);
        await Assert.That(html).IsNotEmpty();
    }
}
