using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Forum;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionForumPostTests {
    private const string PortalUrl = "https://qm.test/AssemblyMotions/1";
    private const string WikiUrl = "https://wiki.test/Antrag:PA001";

    private static AssemblyMotionForumInput Input(Action<AssemblyMotion>? change = null, string? wikiUrl = WikiUrl, string? resultLabel = null) {
        var motion = new AssemblyMotion {
            Number = 1,
            Title = "Titel des Antrags",
            Section = "Umwelt",
            Summary = "Kurz gesagt.",
            TextMarkdown = "Der Parteitag möge beschließen.",
            ReasonMarkdown = "Weil es gut ist.",
            Status = AssemblyMotionStatus.Submitted,
            Revision = 1
        };
        change?.Invoke(motion);
        var type = new AssemblyMotionType { Code = "PA", Name = "Programmantrag" };
        return new AssemblyMotionForumInput(motion, type, "Bundesparteitag 2026.2", ["Anna Pirat", "Bernd Beispiel"], "Eingereicht", resultLabel, PortalUrl, wikiUrl);
    }

    private static async Task AssertInOrder(string text, params string[] parts) {
        var position = -1;
        foreach (var part in parts) {
            var next = text.IndexOf(part, position + 1, StringComparison.Ordinal);
            await Assert.That(next).IsGreaterThan(position);
            position = next;
        }
    }

    [Test]
    public async Task Full_post_contains_all_parts_in_order() {
        var raw = AssemblyMotionForumPost.Raw(Input(resultLabel: "Angenommen"), 32000);

        await AssertInOrder(raw,
            "Programmantrag PA001: Titel des Antrags",
            "Antragsteller: Anna Pirat, Bernd Beispiel",
            "Status: Eingereicht",
            "Ergebnis: Angenommen",
            "Antragsgruppe: Umwelt",
            "### Zusammenfassung\nKurz gesagt.",
            "[details=\"Antragstext\"]\nDer Parteitag möge beschließen.\n[/details]",
            "[details=\"Begründung\"]\nWeil es gut ist.\n[/details]",
            "Antrag im Antragsportal: " + PortalUrl,
            "Antrag im Wiki: " + WikiUrl,
            "_Dieser Beitrag wird automatisch aus dem Antragsportal aktualisiert, Änderungen hier werden überschrieben._");
    }

    [Test]
    public async Task Missing_summary_shows_the_notice() {
        var raw = AssemblyMotionForumPost.Raw(Input(m => m.Summary = null), 32000);

        await Assert.That(raw).Contains("### Zusammenfassung\nFür diesen Antrag wurde keine Zusammenfassung angegeben.");
    }

    [Test]
    public async Task Missing_section_and_result_have_no_line() {
        var raw = AssemblyMotionForumPost.Raw(Input(m => m.Section = null), 32000);

        await Assert.That(raw).DoesNotContain("Antragsgruppe:");
        await Assert.That(raw).DoesNotContain("Ergebnis:");
    }

    [Test]
    public async Task Revision_line_only_while_in_revision() {
        var submitted = AssemblyMotionForumPost.Raw(Input(), 32000);
        var inRevision = AssemblyMotionForumPost.Raw(Input(m => m.Status = AssemblyMotionStatus.CollectingCoSubmitters), 32000);

        await Assert.That(submitted).DoesNotContain("überarbeitet");
        await Assert.That(inRevision).Contains("**Dieser Antrag wird überarbeitet und sucht erneut Mitantragsteller.**");
    }

    [Test]
    public async Task Wiki_line_only_with_wiki_url() {
        var raw = AssemblyMotionForumPost.Raw(Input(wikiUrl: null), 32000);

        await Assert.That(raw).DoesNotContain("Antrag im Wiki");
    }

    [Test]
    public async Task Too_long_text_is_replaced_by_notice() {
        var raw = AssemblyMotionForumPost.Raw(Input(m => m.TextMarkdown = new string('a', 40000)), 32000);

        await Assert.That(raw).Contains("Der Antragstext ist zu lang für das Forum und steht vollständig im Antragsportal und im Wiki.");
        await Assert.That(raw).Contains("Weil es gut ist.");
        await Assert.That(raw.Length).IsLessThanOrEqualTo(32000);
    }

    [Test]
    public async Task Too_long_text_and_reason_are_both_replaced() {
        var raw = AssemblyMotionForumPost.Raw(Input(m => {
            m.TextMarkdown = new string('a', 20000);
            m.ReasonMarkdown = new string('b', 20000);
        }), 15000);

        await Assert.That(raw).Contains("Der Antragstext ist zu lang");
        await Assert.That(raw).Contains("Die Begründung ist zu lang für das Forum und steht vollständig im Antragsportal und im Wiki.");
    }

    [Test]
    public async Task Text_that_fits_keeps_text_and_drops_only_the_reason() {
        var raw = AssemblyMotionForumPost.Raw(Input(m => {
            m.TextMarkdown = new string('a', 5000);
            m.ReasonMarkdown = new string('b', 20000);
        }), 15000);

        await Assert.That(raw).Contains(new string('a', 5000));
        await Assert.That(raw).Contains("Die Begründung ist zu lang");
    }

    [Test]
    public async Task Force_notices_replaces_both() {
        var raw = AssemblyMotionForumPost.Raw(Input(), 32000, forceNotices: true);

        await Assert.That(raw).DoesNotContain("Der Parteitag möge beschließen.");
        await Assert.That(raw).Contains("Der Antragstext ist zu lang");
        await Assert.That(raw).Contains("Die Begründung ist zu lang");
    }

    [Test]
    public async Task Notices_without_wiki_mention_portal_only() {
        var raw = AssemblyMotionForumPost.Raw(Input(wikiUrl: null), 32000, forceNotices: true);

        await Assert.That(raw).Contains("Der Antragstext ist zu lang für das Forum und steht vollständig im Antragsportal.");
        await Assert.That(raw).DoesNotContain("und im Wiki");
    }

    [Test]
    public async Task Mentions_are_defused_everywhere() {
        var raw = AssemblyMotionForumPost.Raw(Input(m => {
            m.Title = "Antrag von @anna";
            m.Summary = "Fragen an @bernd.b";
            m.TextMarkdown = "@carla soll prüfen, Kontakt mail@example.org";
            m.ReasonMarkdown = "(@dora)";
        }), 32000);
        var title = AssemblyMotionForumPost.Title(Input(m => m.Title = "Antrag von @anna"));

        await Assert.That(raw).Contains("Antrag von \\@anna");
        await Assert.That(raw).Contains("Fragen an \\@bernd.b");
        await Assert.That(raw).Contains("\\@carla soll prüfen");
        await Assert.That(raw).Contains("mail@example.org");
        await Assert.That(raw).Contains("(\\@dora)");
        await Assert.That(title).IsEqualTo("PA001: Antrag von @anna (Bundesparteitag 2026.2)");
    }

    [Test]
    public async Task Details_closing_tag_in_text_cannot_end_the_block() {
        var raw = AssemblyMotionForumPost.Raw(Input(m => m.ReasonMarkdown = "Davor\n[/details]\nDanach [/DETAILS] Ende"), 32000);

        await Assert.That(raw).Contains("Davor\n&#91;/details]\nDanach &#91;/DETAILS] Ende\n[/details]");
    }

    [Test]
    public async Task Title_is_built_from_number_title_and_event() {
        await Assert.That(AssemblyMotionForumPost.Title(Input())).IsEqualTo("PA001: Titel des Antrags (Bundesparteitag 2026.2)");
    }

    [Test]
    public async Task Title_is_cut_to_255_keeping_number_and_event() {
        var title = AssemblyMotionForumPost.Title(Input(m => m.Title = new string('x', 500)));

        await Assert.That(title.Length).IsEqualTo(255);
        await Assert.That(title).StartsWith("PA001: ");
        await Assert.That(title).EndsWith("... (Bundesparteitag 2026.2)");
    }

    [Test]
    public async Task Hash_changes_with_title_or_raw() {
        var hash = AssemblyMotionForumPost.Hash("Titel", "Text");

        await Assert.That(AssemblyMotionForumPost.Hash("Titel", "Text")).IsEqualTo(hash);
        await Assert.That(AssemblyMotionForumPost.Hash("Titel 2", "Text")).IsNotEqualTo(hash);
        await Assert.That(AssemblyMotionForumPost.Hash("Titel", "Text 2")).IsNotEqualTo(hash);
    }

    [Test]
    [Arguments("https://wiki.piratenpartei.de/$1", "Antrag:BPT 2026.2/PA001", "https://wiki.piratenpartei.de/Antrag:BPT_2026.2/PA001")]
    [Arguments("https://wiki.example.org/wiki/$1", "Antrag:PA001", "https://wiki.example.org/wiki/Antrag:PA001")]
    [Arguments("https://wiki.example.org/wiki/$1", "Antrag:Was?/Ä&B#1", "https://wiki.example.org/wiki/Antrag:Was%3F/%C3%84%26B%231")]
    public async Task Wiki_page_url_follows_the_article_path(string articlePath, string title, string expected) {
        await Assert.That(AssemblyMotionWikiPage.Url(articlePath, title)).IsEqualTo(expected);
    }
    [Test]
    public async Task Escaped_backslash_before_mention_cannot_revive_it() {
        var raw = AssemblyMotionForumPost.Raw(Input(m => m.TextMarkdown = "a \\@anna b \\\\@bernd c"), 32000);

        await Assert.That(raw).Contains("a \\@anna b");
        await Assert.That(raw).Contains("\\\\\\@bernd c");
    }

    [Test]
    public async Task Details_tags_in_text_are_neutralised() {
        var raw = AssemblyMotionForumPost.Raw(Input(m => m.ReasonMarkdown = "x \\[/details] y [details=\"z\"] w </details> v <DETAILS> u <summary>"), 32000);
        var reason = raw[raw.IndexOf("[details=\"Begründung\"]", StringComparison.Ordinal)..];

        await Assert.That(reason).Contains("x \\&#91;/details] y &#91;details=\"z\"] w &lt;/details> v &lt;DETAILS> u &lt;summary>");
        await Assert.That(reason.Split("[details").Length).IsEqualTo(2);
        await Assert.That(reason.Split("[/details]").Length).IsEqualTo(2);
    }
}
