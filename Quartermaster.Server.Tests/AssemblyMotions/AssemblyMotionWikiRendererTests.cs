using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionWikiRendererTests {
    private const string PortalUrl = "https://qm.test/AssemblyMotions/abc";
    private const string BotUserName = "QM-Bot";

    private static AssemblyMotionPortal Portal() => new() {
        WikiPagePrefix = "Antrag:Bundesparteitag 2027.1/Antragsportal",
        WikiAssemblyLabel = "Bundesparteitag 2027.1"
    };

    private static AssemblyMotionType Type(string code = "SÄA", string name = "Satzungsänderungsantrag") => new() {
        Code = code,
        Name = name
    };

    private static AssemblyMotion Motion(AssemblyMotionStatus status = AssemblyMotionStatus.Submitted) => new() {
        Number = 7,
        Title = "Beispielantrag",
        Section = "Allgemeines",
        TextMarkdown = "Der Parteitag moege beschliessen.",
        ReasonMarkdown = "Weil es sinnvoll ist.",
        Summary = "Kurz",
        Keywords = "a, b",
        PriorDiscussion = "Mailingliste",
        Status = status
    };

    private static AssemblyMotionWikiInput Input(
        AssemblyMotion? motion = null, AssemblyMotionType? type = null, bool hasOpenInquiry = false, params string[] competing)
        => new(Portal(), type ?? Type(), motion ?? Motion(), hasOpenInquiry, competing, PortalUrl, BotUserName);

    [Test]
    [Arguments(AssemblyMotionStatus.Withdrawn, false, 6)]
    [Arguments(AssemblyMotionStatus.Lapsed, false, 6)]
    [Arguments(AssemblyMotionStatus.Admitted, false, 2)]
    [Arguments(AssemblyMotionStatus.Admitted, true, 2)]
    [Arguments(AssemblyMotionStatus.FormallyInsufficient, false, 5)]
    [Arguments(AssemblyMotionStatus.Submitted, false, 1)]
    [Arguments(AssemblyMotionStatus.Submitted, true, 7)]
    public async Task Check_icon_maps_status(AssemblyMotionStatus status, bool hasOpenInquiry, int expected) {
        var icon = AssemblyMotionWikiStatus.CheckIcon(Motion(status), hasOpenInquiry);

        await Assert.That(icon).IsEqualTo(expected);
    }

    [Test]
    public async Task Collecting_with_number_maps_like_submitted() {
        var motion = Motion(AssemblyMotionStatus.CollectingCoSubmitters);

        await Assert.That(motion.IsInRevision).IsTrue();
        await Assert.That(AssemblyMotionWikiStatus.CheckIcon(motion, false)).IsEqualTo(1);
        await Assert.That(AssemblyMotionWikiStatus.CheckIcon(motion, true)).IsEqualTo(7);
        await Assert.That(AssemblyMotionWikiStatus.VoteResult(motion)).IsEqualTo(1);
    }

    [Test]
    [Arguments(AssemblyMotionStatus.Withdrawn, AssemblyMotionResult.None, 4)]
    [Arguments(AssemblyMotionStatus.Lapsed, AssemblyMotionResult.None, 4)]
    [Arguments(AssemblyMotionStatus.FormallyInsufficient, AssemblyMotionResult.None, 8)]
    [Arguments(AssemblyMotionStatus.Admitted, AssemblyMotionResult.Accepted, 2)]
    [Arguments(AssemblyMotionStatus.Admitted, AssemblyMotionResult.Rejected, 3)]
    [Arguments(AssemblyMotionStatus.Admitted, AssemblyMotionResult.AcceptedModified, 5)]
    [Arguments(AssemblyMotionStatus.Admitted, AssemblyMotionResult.None, 1)]
    [Arguments(AssemblyMotionStatus.Admitted, AssemblyMotionResult.NotHandled, 1)]
    [Arguments(AssemblyMotionStatus.Submitted, AssemblyMotionResult.None, 1)]
    public async Task Vote_result_maps_status_and_result(AssemblyMotionStatus status, AssemblyMotionResult result, int expected) {
        var motion = Motion(status);
        motion.Result = result;

        await Assert.That(AssemblyMotionWikiStatus.VoteResult(motion)).IsEqualTo(expected);
    }

    [Test]
    public async Task Title_builds_from_prefix_and_number() {
        var title = AssemblyMotionWikiPage.Title(Portal(), Type(), Motion());

        await Assert.That(title).IsEqualTo("Antrag:Bundesparteitag 2027.1/Antragsportal/SÄA007");
    }

    [Test]
    public async Task Default_template_renders_template_call() {
        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input());

        await Assert.That(page).StartsWith("{{Satzungsänderungsantrag BPT\n|bundesparteitag=Bundesparteitag 2027.1");
        await Assert.That(page).EndsWith("\n}}");
        await Assert.That(page).Contains("|titel=Beispielantrag\n");
        await Assert.That(page).Contains("|prüficon=1\n");
        await Assert.That(page).Contains("|abstimmung=1\n");
    }

    [Test]
    public async Task Values_are_not_html_encoded() {
        var motion = Motion();
        motion.Title = "A & B";

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(motion));

        await Assert.That(page).Contains("|titel=A &amp; B\n");
        await Assert.That(page).DoesNotContain("&amp;amp;");
    }

    [Test]
    public async Task In_revision_motion_renders_notice_and_current_text() {
        var motion = Motion(AssemblyMotionStatus.CollectingCoSubmitters);

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(motion));

        await Assert.That(page).StartsWith("'''Dieser Antrag wird überarbeitet");
        await Assert.That(page).Contains(PortalUrl);
        await Assert.That(page).Contains("\n\n{{Satzungsänderungsantrag BPT\n");
        await Assert.That(page).Contains("Der Parteitag moege beschliessen.");
    }

    [Test]
    public async Task Rendered_page_contains_neither_author_id_nor_submitter_parameters() {
        var motion = Motion();
        motion.CreatedByMemberId = Guid.NewGuid();

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(motion));

        await Assert.That(page).DoesNotContain(motion.CreatedByMemberId.ToString());
        await Assert.That(page).DoesNotContain("antragstellerID");
        await Assert.That(page).DoesNotContain("unterstützer");
    }

    [Test]
    public async Task Template_syntax_in_member_values_is_inert() {
        var motion = Motion();
        motion.Title = "{{ motion.PortalUrl }} {% if true %}x{% endif %}";
        motion.TextMarkdown = "{{ motion.PortalUrl }} {% if true %}x{% endif %}";

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(motion));

        await Assert.That(CountOccurrences(page, PortalUrl)).IsEqualTo(1);
        await Assert.That(page).Contains("motion.PortalUrl");
        await Assert.That(page).Contains("if true");
        await Assert.That(page).Contains("endif");
        await Assert.That(page.Split('\n').First(l => l.StartsWith("|titel="))).Contains("motion.PortalUrl");
        await Assert.That(page.Split('\n').First(l => l.StartsWith("|titel="))).DoesNotContain("{%");
    }

    [Test]
    public async Task Template_with_huge_loop_fails_instead_of_running_long() {
        var type = Type();
        type.WikiTemplate = "{% for i in (1..100000) %}{% for j in (1..100000) %}x{% endfor %}{% endfor %}";

        await Assert.That(async () => await AssemblyMotionWikiRenderer.RenderAsync(Input(type: type)))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Default_template_lookup_normalises_type_code_to_nfc() {
        var decomposed = "SA\u0308A";

        await Assert.That(AssemblyMotionWikiTemplates.DefaultFor(decomposed)).IsEqualTo(AssemblyMotionWikiTemplates.DefaultFor("SÄA"));
        await Assert.That(AssemblyMotionWikiTemplates.DefaultFor(decomposed)).Contains("Satzungsänderungsantrag BPT");
    }

    [Test]
    public async Task Hostile_title_cannot_close_template() {
        var motion = Motion();
        motion.Title = "}}{{X";

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(motion));

        await Assert.That(CountOccurrences(page, "{{")).IsEqualTo(1);
        await Assert.That(CountOccurrences(page, "}}")).IsEqualTo(1);
        await Assert.That(page).Contains("|titel=&#125;&#125;&#123;&#123;X\n");
    }

    [Test]
    public async Task Hostile_values_cannot_inject_parameters() {
        var motion = Motion();
        motion.Title = "x\n|text=evil | y";
        motion.Keywords = "a|b";

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(motion));

        var parameterLines = page.Split('\n').Where(l => l.StartsWith('|')).ToList();
        await Assert.That(parameterLines.Count(l => l.StartsWith("|text="))).IsEqualTo(1);
        await Assert.That(page.Split('\n').Where(l => !l.StartsWith('|') && l.Contains('|'))).IsEmpty();
        await Assert.That(page).DoesNotContain("evil |");
    }

    [Test]
    public async Task Empty_template_falls_back_to_default() {
        var type = Type("GP", "Grundsatzprogramm");
        type.WikiTemplate = "   ";

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(type: type));

        await Assert.That(page).StartsWith("{{Programmantrag BPT\n");
    }

    [Test]
    public async Task Custom_template_is_used_when_set() {
        var type = Type();
        type.WikiTemplate = "Titel: {{ motion.Title }} / {{ assembly.Label }}";

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(type: type));

        await Assert.That(page).IsEqualTo("Titel: Beispielantrag / Bundesparteitag 2027.1");
    }

    [Test]
    public async Task Broken_template_throws_with_fluid_error() {
        var type = Type();
        type.WikiTemplate = "{% if motion.Title %}unclosed";

        var exception = await Assert.That(async () => await AssemblyMotionWikiRenderer.RenderAsync(Input(type: type)))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("Template parse error");
    }

    [Test]
    public async Task Line_endings_and_nfc_are_normalised() {
        var type = Type();
        type.WikiTemplate = "A\r\nB\rC é {{ motion.Title }}   \r\n\r\n  ";

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(type: type));

        await Assert.That(page).IsEqualTo("A\nB\nC é Beispielantrag");
        await Assert.That(page.IsNormalized()).IsTrue();
    }

    [Test]
    public async Task Default_page_has_only_lf_and_no_trailing_whitespace() {
        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input());

        await Assert.That(page).DoesNotContain("\r");
        await Assert.That(page).IsEqualTo(page.TrimEnd());
    }

    [Test]
    [Arguments("GP", "Grundsatzprogramm", "Programmantrag BPT")]
    [Arguments("WP", "Wahlprogramm", "Programmantrag BPT")]
    [Arguments("SÄA", "Satzungsänderungsantrag", "Satzungsänderungsantrag BPT")]
    [Arguments("SBA", "Satzungsbeiordnung", "Sonstiger Antrag BPT")]
    [Arguments("PP", "Positionspapier", "Sonstiger Antrag BPT")]
    [Arguments("SO", "Sonstiger Antrag", "Sonstiger Antrag BPT")]
    public async Task All_default_templates_render_for_all_six_default_type_codes(string code, string name, string expectedTemplate) {
        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(type: Type(code, name)));

        await Assert.That(page).StartsWith("{{" + expectedTemplate + "\n");
        await Assert.That(page).EndsWith("\n}}");
        await Assert.That(page.Contains("|artantrag=" + name + "\n")).IsEqualTo(expectedTemplate == "Sonstiger Antrag BPT");
    }

    [Test]
    public async Task Competing_numbers_are_comma_separated() {
        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(competing: ["SÄA002", "SÄA011"]));

        await Assert.That(page).Contains("|konkurrenz=SÄA002, SÄA011\n");
    }

    [Test]
    public async Task Portal_url_and_bot_name_are_inserted_verbatim() {
        var input = Input() with {
            PortalUrl = "https://qm.test/AssemblyMotions/a-b?x=1&y=2",
            BotUserName = "Antragsportal Bot"
        };

        var page = await AssemblyMotionWikiRenderer.RenderAsync(input);

        await Assert.That(page).Contains("{{Satzungsänderungsantrag BPT");
        await Assert.That(page).Contains("einsehbar (Login erforderlich): https://qm.test/AssemblyMotions/a-b?x=1&y=2\n");
        await Assert.That(page).Contains("|wikiBenutzer=Antragsportal Bot\n");
    }

    [Test]
    public async Task Multi_block_text_renders_as_lines_starting_with_tag() {
        var motion = Motion();
        motion.TextMarkdown = "Erster Absatz.\n\nZweiter Absatz.\n\n- Punkt eins\n- Punkt zwei";

        var page = await AssemblyMotionWikiRenderer.RenderAsync(Input(motion));

        var lines = page.Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith("|text="));
        var end = Array.FindIndex(lines, start + 1, l => l.StartsWith('|'));
        var block = lines.Skip(start).Take(end - start).ToList();
        await Assert.That(block.Count).IsGreaterThan(2);
        await Assert.That(block[0]).StartsWith("|text=<");
        await Assert.That(block.Skip(1).All(l => l.StartsWith('<'))).IsTrue();
    }

    private static int CountOccurrences(string text, string needle) {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0) {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
