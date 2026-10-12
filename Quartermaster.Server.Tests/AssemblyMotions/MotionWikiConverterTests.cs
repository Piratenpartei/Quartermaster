using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Quartermaster.Rendering;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public partial class MotionWikiConverterTests {
    private static string[] Fragments { get; } = [
        "|", "}}", "{{x}}", "{{subst:T}}", "{{{p}}}", "{{#if:x|y}}", "[[Category:X]]", "[[File:X.png]]", "]]", "[[",
        "</nowiki>", "<nowiki>", "<ref>a</ref>", "<pre>", "</pre>", "__TOC__", "__NOINDEX__", "~~~", "~~~~~", "''x''",
        "'''x'''", "<script>alert(1)</script>", "<div onclick=\"x\">y</div>", "<!-- c -->", "ISBN 1234567890", "RFC 1234",
        "https://example.org/a|b", "&#124;", "&lbrace;", "|}", "{|", "!!", "||", "----", "<gallery>"
    ];

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTagRegex();

    [GeneratedRegex(@"^<(\/?(p|strong|em|del|s|ul|ol|li|h[1-6]|blockquote|pre|code|table|tr|th|td)|br \/|hr \/)>$")]
    private static partial Regex AllowedTagRegex();

    [GeneratedRegex(@"\[(?=(https?:\/\/|mailto:))", RegexOptions.IgnoreCase)]
    private static partial Regex LinkOpenRegex();

    private static List<string> Violations(string output) {
        var violations = new List<string>();
        foreach (var forbidden in new[] { "{", "}", "|", "[[", "]]", "''", "~~~", "__" }) {
            if (output.Contains(forbidden))
                violations.Add($"contains {forbidden}");
        }

        foreach (Match match in AnyTagRegex().Matches(output)) {
            if (!AllowedTagRegex().IsMatch(match.Value))
                violations.Add($"tag {match.Value}");
        }

        var withoutTags = AnyTagRegex().Replace(output, "");
        if (withoutTags.Contains('<') || withoutTags.Contains('>'))
            violations.Add("stray angle bracket");

        var linkCount = LinkOpenRegex().Matches(output).Count;
        if (output.Count(c => c == '[') != linkCount || output.Count(c => c == ']') != linkCount)
            violations.Add("unbalanced or foreign brackets");

        AddLineViolations(output, violations);
        if (!output.IsNormalized(NormalizationForm.FormC))
            violations.Add("not NFC");
        if (!("=" + output).IsNormalized(NormalizationForm.FormC))
            violations.Add("not NFC after =");

        return violations;
    }

    private static void AddLineViolations(string output, List<string> violations) {
        if (output.Length == 0)
            return;

        var inPre = false;
        foreach (var line in output.Split('\n')) {
            if (inPre) {
                if (line.Length > 0 && " *#:;={!-".Contains(line[0]))
                    violations.Add($"pre line starts with '{line[0]}'");
            } else if (!line.StartsWith('<')) {
                violations.Add($"line does not start with a tag: {line}");
            }

            if (line.StartsWith("<pre>"))
                inPre = true;
            if (line.Contains("</pre>"))
                inPre = false;
        }
    }

    private static async Task AssertSafe(string output) {
        await Assert.That(Violations(output)).IsEmpty();
    }

    [Test]
    public async Task Bold_and_italic_become_html_tags() {
        await Assert.That(MotionWikiConverter.ToWikitext("**fett** und *kursiv*"))
            .IsEqualTo("<p><strong>fett</strong> und <em>kursiv</em></p>");
    }

    [Test]
    public async Task Strikethrough_becomes_del() {
        await Assert.That(MotionWikiConverter.ToWikitext("~~alt~~")).IsEqualTo("<p><del>alt</del></p>");
    }

    [Test]
    public async Task Heading_becomes_h1() {
        await Assert.That(MotionWikiConverter.ToWikitext("# Titel")).IsEqualTo("<h1>Titel</h1>");
    }

    [Test]
    public async Task Nested_list_nests_ul_inside_li() {
        var output = MotionWikiConverter.ToWikitext("- a\n  - b\n- c");
        await Assert.That(output).IsEqualTo("<ul>\n<li>a\n<ul>\n<li>b</li></ul></li>\n<li>c</li></ul>");
    }

    [Test]
    public async Task Link_with_space_in_url_is_percent_encoded() {
        await Assert.That(MotionWikiConverter.ToWikitext("[Satzung](<https://example.org/a b>)"))
            .IsEqualTo("<p>[https://example.org/a%20b Satzung]</p>");
    }

    [Test]
    public async Task Link_keeps_a_working_query_string() {
        await Assert.That(MotionWikiConverter.ToWikitext("[x](https://example.org/?a=1&b=2)"))
            .IsEqualTo("<p>[https://example.org/?a=1&b=2 x]</p>");
    }

    [Test]
    public async Task Table_uses_html_without_attributes_and_escapes_pipes() {
        var output = MotionWikiConverter.ToWikitext("| a | b |\n|---|---|\n| 1 \\| x | 2 |");
        await Assert.That(output)
            .IsEqualTo("<table>\n<tr>\n<th>a</th>\n<th>b</th></tr>\n<tr>\n<td>1 &#124; x</td>\n<td>2</td></tr></table>");
    }

    [Test]
    public async Task Attributes_are_dropped() {
        var output = MotionWikiConverter.ToWikitext("| a | b |\n|---|---:|\n| 1 | 2 |");
        await Assert.That(output).DoesNotContain("style");
        await Assert.That(output).DoesNotContain("align");
        await Assert.That(output).Contains("<td>2</td>");
    }

    [Test]
    public async Task Inline_code_escapes_pipe_and_ampersand() {
        await Assert.That(MotionWikiConverter.ToWikitext("`x|y&z`")).IsEqualTo("<p><code>x&#124;y&amp;z</code></p>");
    }

    [Test]
    public async Task Fenced_code_becomes_pre_with_entities() {
        var output = MotionWikiConverter.ToWikitext("```\na|b <x> &amp;\n  {{y}}\n```");
        await Assert.That(output).IsEqualTo("<pre>a&#124;b &#60;x&#62; &amp;amp&#59;\n&#32; &#123;&#123;y&#125;&#125;</pre>");
    }

    [Test]
    public async Task Code_block_with_closing_tags_cannot_break_out() {
        var output = MotionWikiConverter.ToWikitext("```\n</pre></code>{{x}}\n```");
        await Assert.That(output).IsEqualTo("<pre>&#60;/pre&#62;&#60;/code&#62;&#123;&#123;x&#125;&#125;</pre>");
        await AssertSafe(output);
    }

    [Test]
    public async Task Blockquote_and_list_nest() {
        var output = MotionWikiConverter.ToWikitext("> a\n>\n> > b\n>\n> - c\n>   - d");
        await Assert.That(output).StartsWith("<blockquote>");
        await Assert.That(output).Contains("<blockquote>\n<p>b</p></blockquote>");
        await Assert.That(output).Contains("<ul>");
        await AssertSafe(output);
    }

    [Test]
    public async Task Thematic_break_becomes_hr() {
        await Assert.That(MotionWikiConverter.ToWikitext("a\n\n---\n\nb")).IsEqualTo("<p>a</p>\n<hr />\n<p>b</p>");
    }

    [Test]
    public async Task Hard_break_becomes_br_without_newline() {
        await Assert.That(MotionWikiConverter.ToWikitext("a  \nb")).IsEqualTo("<p>a<br />b</p>");
    }

    [Test]
    public async Task Null_and_empty_return_empty() {
        await Assert.That(MotionWikiConverter.ToWikitext(null)).IsEqualTo("");
        await Assert.That(MotionWikiConverter.ToWikitext("")).IsEqualTo("");
        await Assert.That(MotionWikiConverter.EscapeInline(null)).IsEqualTo("");
        await Assert.That(MotionWikiConverter.EscapeInline("")).IsEqualTo("");
    }

    [Test]
    [Arguments("a | text=injected")]
    [Arguments("}}{{Löschantrag}}")]
    [Arguments("{{subst:Vorlage}}")]
    [Arguments("</nowiki>{{X}}")]
    [Arguments("[[Kategorie:Spam]]")]
    [Arguments("<ref>x</ref>")]
    [Arguments("__NOTOC__")]
    [Arguments("__TOC__ and __NOINDEX__")]
    [Arguments("~~~~")]
    [Arguments("~~~ and ~~~~~")]
    [Arguments("''''")]
    [Arguments("<div onclick=\"x\">{{x}}</div>")]
    [Arguments("[link](javascript:alert(1))")]
    [Arguments("{{{param}}}")]
    [Arguments("{{#if:x|y}}")]
    [Arguments("[[File:X.png]]")]
    [Arguments("`<!-- comment -->`")]
    [Arguments("<!-- comment -->")]
    [Arguments("<nowiki>x</nowiki> <pre>y</pre> <syntaxhighlight>z</syntaxhighlight> <math>1</math> <gallery>a</gallery> <includeonly>i</includeonly>")]
    [Arguments("&#124;&#124; &lbrace;&lbrace;x&rbrace;&rbrace;")]
    public async Task Hostile_input_is_neutralised(string markdown) {
        var output = MotionWikiConverter.ToWikitext(markdown);
        await AssertSafe(output);
    }

    [Test]
    public async Task Typed_entities_stay_literal() {
        await Assert.That(MotionWikiConverter.ToWikitext("&#124; &lbrace;")).IsEqualTo("<p>&#124; &#123;</p>");
        await Assert.That(MotionWikiConverter.ToWikitext("&amp;#124; &amp;lbrace;")).IsEqualTo("<p>&amp;&#35;124&#59; &amp;lbrace&#59;</p>");
    }

    [Test]
    public async Task Link_url_with_hostile_characters_is_encoded() {
        var output = MotionWikiConverter.ToWikitext("[x](<https://example.org/a}}b|c]d e\"f'g\\<script\\>h>)");
        await Assert.That(output).IsEqualTo("<p>[https://example.org/a%7D%7Db%7Cc%5Dd%20e%22f%27g%3Cscript%3Eh x]</p>");
        await AssertSafe(output);
    }

    [Test]
    public async Task Link_label_with_brackets_pipes_and_templates_is_escaped() {
        var output = MotionWikiConverter.ToWikitext("[a \\] b \\| {{x}}](https://example.org/)");
        await Assert.That(output).IsEqualTo("<p>[https://example.org/ a &#93; b &#124; &#123;&#123;x&#125;&#125;]</p>");
    }

    [Test]
    public async Task Autolink_with_pipe_is_encoded() {
        var output = MotionWikiConverter.ToWikitext("<https://example.org/a|b>");
        await Assert.That(output).IsEqualTo("<p>[https://example.org/a%7Cb https&#58;//example.org/a&#124;b]</p>");
    }

    [Test]
    public async Task Mailto_link_is_kept() {
        await Assert.That(MotionWikiConverter.ToWikitext("[Mail](mailto:a@example.org)"))
            .IsEqualTo("<p>[mailto:a@example.org Mail]</p>");
    }

    [Test]
    public async Task Uppercase_scheme_is_kept() {
        await Assert.That(MotionWikiConverter.ToWikitext("[x](HTTPS://EXAMPLE.ORG/)"))
            .IsEqualTo("<p>[HTTPS://EXAMPLE.ORG/ x]</p>");
    }

    [Test]
    [Arguments("[x](//evil.example)", "<p>x</p>")]
    [Arguments("[x](data:text/html,hi)", "<p>x</p>")]
    [Arguments("[x](vbscript:msgbox)", "<p>x</p>")]
    [Arguments("[x](javascript:alert(1))", "<p>x</p>")]
    [Arguments("[x](ftp://example.org/)", "<p>x</p>")]
    public async Task Other_schemes_keep_only_the_label(string markdown, string expected) {
        await Assert.That(MotionWikiConverter.ToWikitext(markdown)).IsEqualTo(expected);
    }

    [Test]
    public async Task Url_with_underscores_and_tildes_is_encoded() {
        var output = MotionWikiConverter.ToWikitext("[x](https://example.org/__TOC__/~~~~)");
        await Assert.That(output).IsEqualTo("<p>[https://example.org/%5F_TOC%5F_/%7E%7E%7E%7E x]</p>");
    }

    [Test]
    public async Task Heading_with_equals_and_pipe_is_escaped() {
        var output = MotionWikiConverter.ToWikitext("## a = b | c ==");
        await Assert.That(output).IsEqualTo("<h2>a &#61; b &#124; c &#61;&#61;</h2>");
    }

    [Test]
    public async Task Table_cell_with_table_syntax_is_escaped() {
        var output = MotionWikiConverter.ToWikitext("| h |\n|---|\n| \\|} {\\| !! \\|\\| |");
        await Assert.That(output).Contains("<td>&#124;&#125; &#123;&#124; &#33;&#33; &#124;&#124;</td>");
        await AssertSafe(output);
    }

    [Test]
    [Arguments("- * x")]
    [Arguments("- # x")]
    [Arguments("- : x")]
    [Arguments("- ; x")]
    [Arguments("- = x =")]
    public async Task List_item_text_with_line_start_markers_stays_after_a_tag(string markdown) {
        var output = MotionWikiConverter.ToWikitext(markdown);
        foreach (var line in output.Split('\n')) {
            await Assert.That(line).StartsWith("<");
        }
    }

    [Test]
    public async Task Leading_space_in_paragraph_and_list_continuation_never_starts_a_line() {
        var output = MotionWikiConverter.ToWikitext("first\n  second\n   third\n\n- item\n   continued");
        foreach (var line in output.Split('\n')) {
            await Assert.That(line).StartsWith("<");
        }
        await Assert.That(output).DoesNotContain("\n ");
    }

    [Test]
    public async Task Dashes_inside_code_span_are_text() {
        await Assert.That(MotionWikiConverter.ToWikitext("`----`")).IsEqualTo("<p><code>----</code></p>");
    }

    [Test]
    public async Task Magic_links_are_neutralised() {
        var output = MotionWikiConverter.ToWikitext("ISBN 1234567890, RFC 1234, PMID 1234, isbn 5");
        await Assert.That(output).IsEqualTo("<p>&#73;SBN 1234567890, &#82;FC 1234, &#80;MID 1234, isbn 5</p>");
    }

    [Test]
    public async Task Bare_url_in_text_is_not_a_link() {
        var output = MotionWikiConverter.ToWikitext("see https://example.org/x and mailto:a@b.c and //evil.example");
        await Assert.That(output).DoesNotContain(":");
        await Assert.That(output).IsEqualTo("<p>see https&#58;//example.org/x and mailto&#58;a@b.c and //evil.example</p>");
    }

    [Test]
    public async Task Emphasis_never_produces_wiki_apostrophes() {
        var output = MotionWikiConverter.ToWikitext("***both*** **b** *i* it's ''x''");
        await Assert.That(output).DoesNotContain("'");
        await AssertSafe(output);
    }

    [Test]
    public async Task Signature_and_magic_word_characters_are_entities() {
        await Assert.That(MotionWikiConverter.ToWikitext("a ~~~ b \\_\\_TOC\\_\\_")).IsEqualTo("<p>a &#126;&#126;&#126; b &#95;&#95;TOC&#95;&#95;</p>");
    }

    [Test]
    public async Task Control_characters_are_removed() {
        var output = MotionWikiConverter.ToWikitext("a\u0000b\u007Fc\u0001d");
        await Assert.That(output).IsEqualTo("<p>a\uFFFDbcd</p>");
    }

    [Test]
    public async Task EscapeInline_neutralises_template_syntax() {
        await Assert.That(MotionWikiConverter.EscapeInline("}}{{X|y=z"))
            .IsEqualTo("&#125;&#125;&#123;&#123;X&#124;y&#61;z");
    }

    [Test]
    public async Task EscapeInline_removes_line_breaks_and_tabs() {
        var output = MotionWikiConverter.EscapeInline("a\r\nb\tc\nd");
        await Assert.That(output).DoesNotContain("\n");
        await Assert.That(output).DoesNotContain("\r");
        await Assert.That(output).DoesNotContain("\t");
        await Assert.That(output).IsEqualTo("a  b c d");
    }

    [Test]
    [Arguments("* a", "&#42; a")]
    [Arguments("# a", "&#35; a")]
    [Arguments("; a : b", "&#59; a &#58; b")]
    [Arguments(": a", "&#58; a")]
    [Arguments("= a =", "&#61; a &#61;")]
    [Arguments("! a", "&#33; a")]
    [Arguments("- a", "&#45; a")]
    [Arguments("----", "&#45;---")]
    [Arguments("{| x", "&#123;&#124; x")]
    [Arguments("  * a", "&#42; a")]
    [Arguments("\u00A0\u00A0 a", "a")]
    [Arguments("\u00A0* a", "&#42; a")]
    [Arguments("* x = y", "&#42; x &#61; y")]
    [Arguments("say \"hi\"", "say &quot;hi&quot;")]
    public async Task EscapeInline_cannot_start_with_wiki_markup(string input, string expected) {
        await Assert.That(MotionWikiConverter.EscapeInline(input)).IsEqualTo(expected);
    }

    [Test]
    public async Task EscapeInline_never_keeps_a_line_break() {
        await Assert.That(MotionWikiConverter.EscapeInline("a\n* b")).DoesNotContain("\n");
    }

    [Test]
    public async Task Pipe_is_always_an_entity() {
        await Assert.That(MotionWikiConverter.ToWikitext("a | b")).IsEqualTo("<p>a &#124; b</p>");
        await Assert.That(MotionWikiConverter.ToWikitext("}}{{X")).IsEqualTo("<p>&#125;&#125;&#123;&#123;X</p>");
        await Assert.That(MotionWikiConverter.ToWikitext("[x](<https://example.org/a b|c>)"))
            .IsEqualTo("<p>[https://example.org/a%20b%7Cc x]</p>");
        await Assert.That(MotionWikiConverter.ToWikitext("| h |\n|---|\n| a \\| b |"))
            .IsEqualTo("<table>\n<tr>\n<th>h</th></tr>\n<tr>\n<td>a &#124; b</td></tr></table>");
        await Assert.That(MotionWikiConverter.ToWikitext("a | b\n\n`c|d`\n\n```\ne|f\n```")).DoesNotContain("{{!}}");
    }

    [Test]
    public async Task Attribute_injection_through_pipe_and_equals_is_inert() {
        var hostile = "x style=\"position:fixed;top:0\" | hello || more !! x";
        var title = MotionWikiConverter.EscapeInline(hostile);
        var text = MotionWikiConverter.ToWikitext(hostile);
        foreach (var output in new[] { title, text }) {
            await Assert.That(output).DoesNotContain("|");
            await Assert.That(output).DoesNotContain("=");
            await Assert.That(output).DoesNotContain("!");
            await Assert.That(output).DoesNotContain("\"");
        }
        await AssertSafe(text);
    }

    [Test]
    [Arguments("I\u0001SBN 1234567890 R\u0001FC 1234", "<p>&#73;SBN 1234567890 &#82;FC 1234</p>")]
    [Arguments("I[](ftp://x)SBN 1234567890 and R[](#)FC 1234", "<p>&#73;SBN 1234567890 and &#82;FC 1234</p>")]
    [Arguments("Dorfchef und Risbn", "<p>Dorfchef und Risbn</p>")]
    [Arguments("see RFC", "<p>see &#82;FC</p>")]
    [Arguments("PMID", "<p>&#80;MID</p>")]
    [Arguments("(ISBN) ISBN: x ISBNx", "<p>(&#73;SBN) &#73;SBN&#58; x ISBNx</p>")]
    [Arguments("ISBN\u00A01234567890", "<p>&#73;SBN 1234567890</p>")]
    public async Task Magic_link_keywords_are_defused_across_node_boundaries(string markdown, string expected) {
        await Assert.That(MotionWikiConverter.ToWikitext(markdown)).IsEqualTo(expected);
    }

    [Test]
    public async Task Magic_link_keywords_are_defused_in_inline_values() {
        await Assert.That(MotionWikiConverter.EscapeInline("I\u0001SBN 1234567890")).IsEqualTo("&#73;SBN 1234567890");
        await Assert.That(MotionWikiConverter.EscapeInline("Dorfchef")).IsEqualTo("Dorfchef");
    }

    [Test]
    public async Task Isbn_in_a_link_url_is_left_alone() {
        await Assert.That(MotionWikiConverter.ToWikitext("[RFC](https://example.org/ISBN)"))
            .IsEqualTo("<p>[https://example.org/ISBN &#82;FC]</p>");
    }

    [Test]
    [Arguments("\u0338foo", "<p>&#x338;foo</p>")]
    [Arguments("&#x338;foo", "<p>&#x338;foo</p>")]
    [Arguments("`a`\u0301", "<p><code>a</code>&#x301;</p>")]
    [Arguments("a\u037E b", "<p>a&#59; b</p>")]
    [Arguments("\u2126 \u212B", "<p>\u03A9 \u00C5</p>")]
    [Arguments("\u1100\u1161\u11A8", "<p>\uAC01</p>")]
    [Arguments("caf" + "e\u0301 au caf" + "e\u0301", "<p>caf\u00E9 au caf\u00E9</p>")]
    [Arguments("a\uD800b", "<p>a\uFFFDb</p>")]
    [Arguments("x \u0301y", "<p>x &#x301;y</p>")]
    public async Task Output_is_stable_under_nfc(string markdown, string expected) {
        var output = MotionWikiConverter.ToWikitext(markdown);
        await Assert.That(output).IsEqualTo(expected);
        await AssertSafe(output);
    }

    [Test]
    [Arguments("{")]
    [Arguments("}")]
    [Arguments("|")]
    [Arguments("*")]
    [Arguments("#")]
    [Arguments(";")]
    [Arguments(":")]
    [Arguments("!")]
    [Arguments("=")]
    [Arguments("\"")]
    [Arguments("~")]
    [Arguments("'")]
    [Arguments("_")]
    [Arguments("&")]
    [Arguments("[")]
    [Arguments("]")]
    [Arguments("<")]
    [Arguments(">")]
    [Arguments(" ")]
    public async Task Combining_marks_after_escaped_characters_become_references(string escapedClass) {
        var title = MotionWikiConverter.EscapeInline("a" + escapedClass + "\u0301");
        await Assert.That(title).Contains("&#x301;");
        await Assert.That(("titel=" + title).IsNormalized(NormalizationForm.FormC)).IsTrue();

        if (escapedClass == " ")
            return;

        var text = MotionWikiConverter.ToWikitext("a" + escapedClass + "\u0301 b");
        await Assert.That(text.IsNormalized(NormalizationForm.FormC)).IsTrue();
        await Assert.That(text).Contains("&#x301;");
    }

    [Test]
    public async Task EscapeInline_is_stable_under_nfc() {
        await Assert.That(MotionWikiConverter.EscapeInline("\u0338foo")).IsEqualTo("&#x338;foo");
        await Assert.That(MotionWikiConverter.EscapeInline("\u037E a")).IsEqualTo("&#59; a");
        await Assert.That(MotionWikiConverter.EscapeInline("\u2126\u212B")).IsEqualTo("\u03A9\u00C5");
        await Assert.That(MotionWikiConverter.EscapeInline("Antra\u0308ge")).IsEqualTo("Antr\u00E4ge");
        await Assert.That(MotionWikiConverter.EscapeInline("a\uDC00b")).IsEqualTo("a\uFFFDb");
        await Assert.That(MotionWikiConverter.EscapeInline("\u0001\u0301x")).IsEqualTo("&#x301;x");
    }

    [Test]
    public async Task Control_characters_do_not_defeat_pre_line_start_encoding() {
        var output = MotionWikiConverter.ToWikitext("```\nx\n\u0001 ctl\n\u0001-dash\n```");
        await Assert.That(output).IsEqualTo("<pre>x\n&#32;ctl\n&#45;dash</pre>");
        await AssertSafe(output);
    }

    [Test]
    public async Task Unicode_marks_in_a_url_are_normalised() {
        var output = MotionWikiConverter.ToWikitext("[x](https://example.org/a\u0308)");
        await Assert.That(output).StartsWith("<p>[https://example.org/");
        await Assert.That(output).DoesNotContain("\u0308");
        await AssertSafe(output);
    }

    [Test]
    public async Task Walker_drops_every_attribute() {
        var html = "<p class=\"c\" style=\"color:red\" onclick=\"x()\"><strong style=\"a\">b</strong></p>"
            + "<ol start=\"5\"><li class=\"z\">i</li></ol>"
            + "<table class=\"t\"><tr><td style=\"x\" colspan=\"2\">c</td></tr></table>"
            + "<pre class=\"language-x\"><code class=\"language-x\">code</code></pre>"
            + "<blockquote><span href=\"https://x.org\" title=\"t\">s</span></blockquote>"
            + "<a title=\"t\" href=\"javascript:alert(1)\" onclick=\"y\">l</a>";

        var output = MotionWikiConverter.ConvertHtml(html);

        foreach (var forbidden in new[] { "style", "class", "start", "href", "onclick", "colspan", "title", "javascript", "language" }) {
            await Assert.That(output).DoesNotContain(forbidden);
        }
        await Assert.That(output).StartsWith("<p><strong>b</strong></p>");
        await AssertSafe(output);
    }

    [Test]
    public async Task Output_is_independent_of_the_current_culture() {
        var original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var output = MotionWikiConverter.ToWikitext("ISBN 1 isbn 2 [x](https://example.org/a b)");
            await Assert.That(output).IsEqualTo("<p>&#73;SBN 1 isbn 2 &#91;x&#93;(https&#58;//example.org/a b)</p>");
        } finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public async Task Nasty_fragments_never_produce_dangerous_output() {
        MotionWikiConverter.ToWikitext("warmup **x** [a](https://example.org) | t\n|-|\n|1|");
        var stopwatch = Stopwatch.StartNew();
        var failures = new List<string>();

        for (var firstIndex = 0; firstIndex < Fragments.Length; firstIndex++) {
            for (var secondIndex = firstIndex; secondIndex < Fragments.Length; secondIndex++) {
                var first = Fragments[firstIndex];
                var second = Fragments[secondIndex];
                var contexts = new[] {
                    $"before {first} middle {second} after",
                    $"- {first} {second}",
                    $"| h |\n|---|\n| {first} {second} |",
                    $"# {first} {second}",
                    $"[{first} {second}](https://example.org/x)",
                    $"[x]({first}{second})"
                };

                foreach (var markdown in contexts) {
                    if (Violations(MotionWikiConverter.ToWikitext(markdown)).Count > 0)
                        failures.Add(markdown);
                }
            }
        }

        await Assert.That(failures).IsEmpty();
        await Assert.That(stopwatch.Elapsed.TotalSeconds).IsLessThan(30);
    }

    [Test]
    public async Task Large_input_converts_in_reasonable_time() {
        var paragraph = "Some **bold** text with a [link](https://example.org/a?b=1) and `code | x` and ISBN 123.\n\n- item {{x}}\n- item [[y]]\n\n";
        var markdown = string.Concat(Enumerable.Repeat(paragraph, 99_000 / paragraph.Length + 1));

        MotionWikiConverter.ToWikitext(paragraph);
        var stopwatch = Stopwatch.StartNew();
        var output = MotionWikiConverter.ToWikitext(markdown);
        stopwatch.Stop();

        await Assert.That(markdown.Length).IsGreaterThanOrEqualTo(98_000);
        await Assert.That(output).IsNotEmpty();
        await Assert.That(stopwatch.Elapsed.TotalSeconds).IsLessThan(30);
    }

    [Test]
    public async Task Deeply_nested_quotes_do_not_overflow_the_stack() {
        var output = MotionWikiConverter.ToWikitext(new string('>', 100) + " deep");
        await Assert.That(output).Contains("deep");
        await AssertSafe(output);
    }

    [Test]
    [Arguments(">", 5000, " deep")]
    [Arguments("> - ", 60, "x")]
    [Arguments("[a", 500, "")]
    public async Task Unrenderable_nesting_converts_to_empty(string unit, int repeat, string tail) {
        var markdown = string.Concat(Enumerable.Repeat(unit, repeat)) + tail;
        await Assert.That(MotionWikiConverter.ToWikitext(markdown)).IsEqualTo("");
    }

    [Test]
    public async Task Too_long_input_converts_to_empty() {
        await Assert.That(MotionWikiConverter.ToWikitext(new string('a', MotionMarkdown.MaxLength + 1))).IsEqualTo("");
    }
}
