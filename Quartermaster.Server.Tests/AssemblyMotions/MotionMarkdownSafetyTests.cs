using System.Collections.Generic;
using System.Linq;
using AngleSharp.Html.Parser;
using Quartermaster.Rendering;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class MotionMarkdownSafetyTests {
    public const string GreekVaria = "`";
    public const string KelvinSign = "K";
    public const string GreekQuestionMark = ";";
    public const string CombiningSolidus = "̸";

    public const string Overlay = "<a href=\"https://evil.example\" class=\"position-fixed top-0 start-0 w-100 h-100\">x</a>";

    private static readonly string[] AllowedTags = [
        "p", "br", "em", "strong", "del", "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6",
        "blockquote", "pre", "code", "hr", "a", "table", "thead", "tbody", "tr", "th", "td"
    ];

    /// <summary>
    /// Texts that are harmless as sent and turn into raw HTML only when they are normalized or trimmed.
    /// </summary>
    public static IEnumerable<string> ShapeShiftingPayloads() {
        yield return "`" + GreekVaria + Overlay + "`";
        yield return "<bloc" + KelvinSign + "quote class=\"position-fixed top-0 start-0 w-100 h-100\">x</bloc" + KelvinSign + "quote>";
        yield return "<" + KelvinSign + "bd onclick=\"alert(1)\">x</" + KelvinSign + "bd> " + Overlay.Replace("<a", "<lin" + KelvinSign);
    }

    /// <summary>
    /// Lists every element or attribute in the HTML that motion rendering must never produce.
    /// </summary>
    public static List<string> ForeignMarkup(string? html) {
        var violations = new List<string>();
        var document = new HtmlParser().ParseDocument(html ?? "");
        foreach (var element in document.Body!.QuerySelectorAll("*")) {
            if (!AllowedTags.Contains(element.LocalName))
                violations.Add($"<{element.LocalName}>");

            foreach (var attribute in element.Attributes) {
                if (element.LocalName != "a" || attribute.Name != "href" || !IsAllowedLink(attribute.Value))
                    violations.Add($"<{element.LocalName} {attribute.Name}=\"{attribute.Value}\">");
            }
        }

        return violations;
    }

    private static bool IsAllowedLink(string href)
        => href.StartsWith("https://") || href.StartsWith("http://") || href.StartsWith("mailto:");

    private static string ToHtml(string markdown)
        => MarkdownService.ToHtml(markdown, SanitizationProfile.Motion, MarkdownFlavor.Motion);

    [Test]
    [MethodDataSource(nameof(ShapeShiftingPayloads))]
    public async Task Text_that_becomes_html_under_normalization_is_a_problem(string markdown) {
        await Assert.That(MotionMarkdown.FindProblems(markdown)).IsEquivalentTo([MotionMarkdownProblem.RawHtml]);
    }

    [Test]
    [MethodDataSource(nameof(ShapeShiftingPayloads))]
    public async Task Text_that_becomes_html_under_normalization_renders_without_foreign_markup(string markdown) {
        var html = ToHtml(markdown);

        await Assert.That(ForeignMarkup(html)).IsEmpty();
        await Assert.That(html).DoesNotContain("position-fixed");
        await Assert.That(html).IsEqualTo(ToHtml(MotionMarkdown.Normalize(markdown)));
    }

    [Test]
    public async Task Greek_question_mark_completes_an_entity_but_no_element() {
        var markdown = "&lt" + GreekQuestionMark + "script&gt" + GreekQuestionMark + "alert(1)";

        var html = ToHtml(markdown);

        await Assert.That(MotionMarkdown.Normalize(markdown)).IsEqualTo("&lt;script&gt;alert(1)");
        await Assert.That(MotionMarkdown.FindProblems(markdown)).IsEmpty();
        await Assert.That(html).IsEqualTo("<p>&lt;script&gt;alert(1)</p>\n");
    }

    [Test]
    public async Task Composition_that_swallows_an_angle_bracket_is_judged_after_normalization() {
        var swallowed = "<b>" + CombiningSolidus + "x";
        var kept = "<b>x<" + CombiningSolidus + "/b>";

        await Assert.That(MotionMarkdown.FindProblems(swallowed)).IsEmpty();
        await Assert.That(ForeignMarkup(ToHtml(swallowed))).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(kept)).IsEquivalentTo([MotionMarkdownProblem.RawHtml]);
        await Assert.That(ForeignMarkup(ToHtml(kept))).IsEmpty();
    }

    [Test]
    public async Task Normalize_yields_nfc_line_feeds_and_no_trailing_whitespace() {
        await Assert.That(MotionMarkdown.Normalize("ä\r\nb\rc\n \n")).IsEqualTo("ä\nb\nc");
        await Assert.That(MotionMarkdown.Normalize("    code")).IsEqualTo("    code");
        await Assert.That(MotionMarkdown.Normalize(null)).IsEqualTo("");
    }

    [Test]
    public async Task Problems_and_rendering_do_not_depend_on_the_normalization_of_the_input() {
        var decomposed = "Überschrift\r\n===\r\n\r\n- Punkt\r- noch einer";
        var normalized = MotionMarkdown.Normalize(decomposed);

        await Assert.That(normalized).IsNotEqualTo(decomposed);
        await Assert.That(MotionMarkdown.FindProblems(decomposed)).IsEquivalentTo(MotionMarkdown.FindProblems(normalized));
        await Assert.That(ToHtml(decomposed)).IsEqualTo(ToHtml(normalized));
        await Assert.That(ToHtml(decomposed)).Contains("<h1>Überschrift</h1>");
    }

    [Test]
    public async Task Length_limit_applies_to_the_normalized_text() {
        var expanding = new string('שּׁ', 40_000);

        await Assert.That(MotionMarkdown.Normalize(expanding).Length).IsGreaterThan(MotionMarkdown.MaxLength);
        await Assert.That(MotionMarkdown.FindProblems(expanding)).IsEquivalentTo([MotionMarkdownProblem.TooLong]);
    }

    [Test]
    public async Task Motion_profile_keeps_only_href_on_links() {
        var html = HtmlSanitizationService.Sanitize(Overlay + "<a href=\"mailto:a@example.org\" id=\"i\" style=\"color:red\" title=\"t\" target=\"_blank\" onclick=\"x()\">m</a>", SanitizationProfile.Motion);

        await Assert.That(html).IsEqualTo("<a href=\"https://evil.example\">x</a><a href=\"mailto:a@example.org\">m</a>");
    }

    [Test]
    [Arguments("javascript:alert(1)")]
    [Arguments("data:text/html,x")]
    [Arguments("/api/users")]
    [Arguments("//evil.example/")]
    [Arguments("#anker")]
    [Arguments("ftp://example.org/")]
    public async Task Motion_profile_drops_links_that_are_not_http_https_or_mailto(string href) {
        var html = HtmlSanitizationService.Sanitize($"<a href=\"{href}\">x</a>", SanitizationProfile.Motion);

        await Assert.That(html).IsEqualTo("<a>x</a>");
    }

    [Test]
    public async Task Motion_profile_drops_wrappers_images_and_every_other_attribute() {
        var html = HtmlSanitizationService.Sanitize(
            "<div class=\"modal\"><span>a</span></div><img src=\"https://example.org/a.png\"><u>b</u>"
            + "<p class=\"c\" style=\"position:fixed\" id=\"i\" href=\"https://example.org\">p</p>"
            + "<table class=\"t\"><tbody><tr><td style=\"text-align: center;\" colspan=\"2\" width=\"9\">z</td></tr></tbody></table>"
            + "<pre><code class=\"language-x\">k</code></pre><ol start=\"3\"><li>l</li></ol>",
            SanitizationProfile.Motion);

        await Assert.That(html).IsEqualTo("<p>p</p><table><tbody><tr><td>z</td></tr></tbody></table><pre><code>k</code></pre><ol><li>l</li></ol>");
    }

    [Test]
    public async Task Motion_flavour_ignores_a_weaker_profile_argument() {
        var markdown = "Text " + Overlay + "\n\n<div class=\"position-fixed\">y</div>";

        var html = MarkdownService.ToHtml(markdown, SanitizationProfile.Standard, MarkdownFlavor.Motion);

        await Assert.That(ForeignMarkup(html)).IsEmpty();
        await Assert.That(html).Contains("<a href=\"https://evil.example\">x</a>");
    }

    [Test]
    public async Task Standard_profile_is_unchanged() {
        var html = HtmlSanitizationService.Sanitize(
            "<a href=\"/intern\" class=\"btn\" target=\"_blank\">x</a><img src=\"https://example.org/a.png\" alt=\"a\" width=\"5\"><td colspan=\"2\">z</td>",
            SanitizationProfile.Standard);

        await Assert.That(html).IsEqualTo("<a href=\"/intern\" class=\"btn\" target=\"_blank\">x</a><img src=\"https://example.org/a.png\" alt=\"a\" width=\"5\">z");
    }

    [Test]
    public async Task Markdown_links_keep_working_in_the_motion_flavour() {
        var html = ToHtml("[a](https://example.org/x?y=1&z=2 \"Titel\") <https://example.org> [m](mailto:a@example.org) [r](/relativ)");

        await Assert.That(html).IsEqualTo("<p><a href=\"https://example.org/x?y=1&amp;z=2\">a</a> <a href=\"https://example.org\">https://example.org</a> <a href=\"mailto:a@example.org\">m</a> <a>r</a></p>\n");
    }
}
