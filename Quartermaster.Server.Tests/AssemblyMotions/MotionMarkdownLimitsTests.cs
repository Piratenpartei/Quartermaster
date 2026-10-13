using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Quartermaster.Rendering;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class MotionMarkdownLimitsTests {
    private static string Repeat(string unit, int count)
        => string.Concat(Enumerable.Repeat(unit, count));

    private static string Table(int columns, int rows)
        => Repeat("|a", columns) + "\n" + Repeat("|-", columns) + "\n" + Repeat("a|\n", rows);

    private static string ToHtml(string markdown)
        => MarkdownService.ToHtml(markdown, SanitizationProfile.Motion, MarkdownFlavor.Motion);

    [Test]
    [Arguments(400, 400)]
    [Arguments(1000, 2000)]
    [Arguments(20000, 4000)]
    public async Task Table_bomb_is_refused_before_parsing(int columns, int rows) {
        var markdown = Table(columns, rows);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        var problems = MotionMarkdown.FindProblems(markdown);
        var html = ToHtml(markdown);
        var wikitext = MotionWikiConverter.ToWikitext(markdown);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        await Assert.That(problems).IsEquivalentTo([MotionMarkdownProblem.TableTooLarge]);
        await Assert.That(html).IsEqualTo("");
        await Assert.That(wikitext).IsEqualTo("");
        await Assert.That(allocated).IsLessThan(2_000_000);
    }

    [Test]
    public async Task Table_cells_are_counted_over_all_tables() {
        var oneTable = Table(10, 20) + "\n";

        await Assert.That(MotionMarkdown.FindProblems(Repeat(oneTable, 40))).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(Repeat(oneTable, 50))).IsEquivalentTo([MotionMarkdownProblem.TableTooLarge]);
    }

    [Test]
    public async Task Large_real_table_is_accepted_and_rendered() {
        var row = "|" + Repeat(" Wert |", 20) + "\n";
        var markdown = row + "|" + Repeat(":-:|", 20) + "\n" + Repeat(row, 250);

        var html = ToHtml(markdown);

        await Assert.That(MotionMarkdown.FindProblems(markdown)).IsEmpty();
        await Assert.That(html.Split("<td>").Length - 1).IsEqualTo(5000);
        await Assert.That(MotionMarkdown.TryRender(markdown, out var unsanitized)).IsTrue();
        await Assert.That(unsanitized).DoesNotContain("style");
    }

    [Test]
    public async Task Rendered_size_above_the_limit_is_refused() {
        var markdown = new string('&', MotionMarkdown.MaxLength);

        await Assert.That(MotionMarkdown.FindProblems(markdown)).IsEquivalentTo([MotionMarkdownProblem.OutputTooLarge]);
        await Assert.That(ToHtml(markdown)).IsEqualTo("");
        await Assert.That(MotionWikiConverter.ToWikitext(markdown)).IsEqualTo("");
    }

    [Test]
    public async Task Rendered_size_limit_applies_to_any_text() {
        var problems = MotionMarkdown.FindProblems("Ein **kurzer** Text", 10);
        var rendered = MotionMarkdown.TryRender("Ein **kurzer** Text", 10, out var html);

        await Assert.That(problems).IsEquivalentTo([MotionMarkdownProblem.OutputTooLarge]);
        await Assert.That(rendered).IsFalse();
        await Assert.That(html).IsEqualTo("");
    }

    [Test]
    public async Task Brackets_are_limited_per_paragraph() {
        var atLimit = Repeat("[a](https://example.org) ", 250);

        await Assert.That(MotionMarkdown.FindProblems(atLimit)).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(atLimit + "[b]")).IsEquivalentTo([MotionMarkdownProblem.TooManyBrackets]);
        await Assert.That(MotionMarkdown.FindProblems(atLimit + "\n\n" + atLimit)).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(atLimit + "\n \t\n" + atLimit)).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(atLimit + "\n" + atLimit)).IsEquivalentTo([MotionMarkdownProblem.TooManyBrackets]);
    }

    [Test]
    public async Task Inline_markers_are_limited_per_paragraph_and_in_total() {
        var fullParagraph = Repeat("**fett** ", 500);

        await Assert.That(MotionMarkdown.FindProblems(fullParagraph)).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(fullParagraph + "*x*")).IsEquivalentTo([MotionMarkdownProblem.TooMuchInlineMarkup]);
        await Assert.That(MotionMarkdown.FindProblems(Repeat(fullParagraph + "\n\n", 10))).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(Repeat(fullParagraph + "\n\n", 10) + "*x*")).IsEquivalentTo([MotionMarkdownProblem.TooMuchInlineMarkup]);
        await Assert.That(MotionMarkdown.FindProblems(Repeat("`a` ", 501))).IsEquivalentTo([MotionMarkdownProblem.TooMuchInlineMarkup]);
        await Assert.That(MotionMarkdown.FindProblems(Repeat("<x ", 1001))).IsEquivalentTo([MotionMarkdownProblem.TooMuchInlineMarkup]);
        await Assert.That(MotionMarkdown.FindProblems(new string('*', 50_000) + "a" + new string('_', 49_000))).IsEmpty();
    }

    [Test]
    public async Task Lines_are_limited() {
        await Assert.That(MotionMarkdown.FindProblems(Repeat("a\n", 4999) + "a")).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(Repeat("a\n", 5000) + "a")).IsEquivalentTo([MotionMarkdownProblem.TooManyLines]);
        await Assert.That(MotionMarkdown.FindProblems(Repeat("a\r\n", 4999) + "a")).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(Repeat("a\r", 5000) + "a")).IsEquivalentTo([MotionMarkdownProblem.TooManyLines]);
    }

    [Test]
    [Arguments("> ")]
    [Arguments(">")]
    public async Task Quote_markers_are_limited_per_line(string marker) {
        await Assert.That(MotionMarkdown.FindProblems(Repeat(marker, 100) + "x")).IsEmpty();
        await Assert.That(MotionMarkdown.FindProblems(Repeat(marker, 101) + "x")).IsEquivalentTo([MotionMarkdownProblem.TooDeeplyNested]);
        await Assert.That(MotionMarkdown.FindProblems(Repeat(Repeat(marker, 100) + "x\n", 20))).IsEmpty();
    }

    [Test]
    [Arguments("- ")]
    [Arguments("+ ")]
    [Arguments("1. ")]
    [Arguments("12) ")]
    [Arguments("* > ")]
    [Arguments(" >\t-  ")]
    public async Task Deep_marker_runs_are_reported_as_nesting(string marker) {
        await Assert.That(MotionMarkdown.FindProblems(Repeat(marker, 101) + "x")).IsEquivalentTo([MotionMarkdownProblem.TooDeeplyNested]);
    }

    [Test]
    public async Task Rules_and_long_marker_like_lines_are_not_nesting() {
        var markdown = new string('-', 300) + "\n\n" + new string('*', 300) + "\n\n" + new string('=', 300) + "\n\n1234567890. Punkt\n\n" + Repeat("-x", 200);

        await Assert.That(MotionMarkdown.FindProblems(markdown)).IsEmpty();
    }

    [Test]
    public async Task Long_ordinary_motion_stays_within_all_limits() {
        var sb = new StringBuilder();
        for (var chapter = 1; sb.Length < 95_000; chapter++) {
            sb.Append("## Kapitel ").Append(chapter).Append("\n\n");
            sb.Append(Repeat("Die Piratenpartei setzt sich für **freie Software**, *offene Standards* und [Transparenz](https://example.org/transparenz) ein. ", 6)).Append("\n\n");
            for (var item = 1; item <= 40; item++)
                sb.Append("- **Punkt ").Append(item).Append("**: Erläuterung mit `Code`, einem [Verweis](https://example.org/").Append(item).Append(") und a < b\n");
            sb.Append("\n| Jahr | Einnahmen | Ausgaben |\n|------|----------:|---------:|\n");
            for (var row = 0; row < 12; row++)
                sb.Append("| 20").Append(10 + row).Append(" | 1.000 | 2.000 |\n");
            sb.Append('\n');
        }
        var markdown = sb.ToString();

        var html = ToHtml(markdown);

        await Assert.That(markdown.Length).IsLessThanOrEqualTo(MotionMarkdown.MaxLength);
        await Assert.That(MotionMarkdown.FindProblems(markdown)).IsEmpty();
        await Assert.That(html).Contains("<strong>Punkt 40</strong>");
    }

    [Test]
    [NotInParallel]
    [Arguments("[a](")]
    [Arguments("[a](b (")]
    [Arguments("![a](")]
    [Arguments("[a][")]
    [Arguments("[")]
    [Arguments("(")]
    [Arguments("*")]
    [Arguments("*a ")]
    [Arguments("*a* **b** ")]
    [Arguments("_")]
    [Arguments("a_ ")]
    [Arguments("~~a ")]
    [Arguments("`")]
    [Arguments("`a")]
    [Arguments("`a` ")]
    [Arguments("> ")]
    [Arguments("> - ")]
    [Arguments("- ")]
    [Arguments("1. ")]
    [Arguments("|*a")]
    [Arguments("#\n")]
    [Arguments("a\n=\n")]
    [Arguments("a  \n")]
    public async Task Pathological_family_at_full_length_is_handled_quickly(string unit) {
        var markdown = Repeat(unit, MotionMarkdown.MaxLength / unit.Length);
        ToHtml("warm up *a* [b](https://example.org) `c`");

        var stopwatch = Stopwatch.StartNew();
        MotionMarkdown.FindProblems(markdown);
        ToHtml(markdown);
        stopwatch.Stop();

        await Assert.That(markdown.Length).IsGreaterThan(MotionMarkdown.MaxLength - 16);
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(1000);
    }

    [Test]
    [NotInParallel]
    [Arguments("[a](b (", 250, 'x')]
    [Arguments("![a](", 250, 'x')]
    [Arguments("[a]((", 250, '(')]
    public async Task Most_expensive_accepted_link_paragraph_is_quick(string unit, int count, char padding) {
        var markdown = Repeat(unit, count) + new string(padding, MotionMarkdown.MaxLength - unit.Length * count);
        ToHtml("warm up *a* [b](https://example.org) `c`");

        var stopwatch = Stopwatch.StartNew();
        var problems = MotionMarkdown.FindProblems(markdown);
        var html = ToHtml(markdown);
        stopwatch.Stop();

        await Assert.That(problems).IsEmpty();
        await Assert.That(html).IsNotEmpty();
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(1000);
    }

    [Test]
    [NotInParallel]
    public async Task Accepted_texts_at_several_limits_are_quick() {
        var markdowns = new[] {
            Repeat(Repeat("*a* ", 500) + "\n\n", 10),
            Repeat(Repeat("`a` ", 500) + "\n\n", 10),
            Repeat("#\n", 4999) + new string('x', 50_000),
            Repeat("a  \n", 4999) + new string('x', 50_000),
            Repeat("- - - a\n", 4999),
            "a|b\n-|-\n" + Repeat("a|\n", 4997),
            Table(99, 98),
            Repeat(Repeat("> ", 100) + "a\n", 490)
        };
        ToHtml("warm up *a* [b](https://example.org) `c`\n\n|a|\n|-|\n|b|");

        foreach (var markdown in markdowns) {
            var stopwatch = Stopwatch.StartNew();
            var problems = MotionMarkdown.FindProblems(markdown);
            var html = ToHtml(markdown);
            stopwatch.Stop();

            await Assert.That(problems).IsEmpty();
            await Assert.That(html).IsNotEmpty();
            await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(1000);
        }
    }

    [Test]
    public async Task Every_problem_has_a_message_key() {
        var keys = Enum.GetValues<MotionMarkdownProblem>()
            .Select(MotionMarkdown.MessageKey)
            .ToList();

        await Assert.That(keys.Distinct().Count()).IsEqualTo(keys.Count);
        await Assert.That(keys.All(key => key.StartsWith("error.assembly_motion.markdown_"))).IsTrue();
    }
}
