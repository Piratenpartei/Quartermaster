using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.Tests.NewsPosts;

public class WikitextSectionSplitterTests {
    private const string Sample = """
        {{Offiziell}}
        <onlyinclude>Einleitung ohne Überschrift</onlyinclude>
        ==Freiheit und Grundrechte==
        === Privatsphäre und Datenschutz ===
        ====Privatsphäre====
        Jedem Bürger muss das Recht auf [[Anonymität|anonyme]] Kommunikation '''garantiert''' werden.<ref>Quelle</ref>
        ====Leer====
        === Jugendschutz ===
        Ein wirksamer Jugendschutz kann nicht durch {{Vorlage|x}} Verbote erreicht werden. Siehe [https://example.org Beispiel].
        """;

    [Test]
    public async Task Splits_at_headings_and_builds_heading_path() {
        var sections = WikitextSectionSplitter.Split("Parteiprogramm", Sample);

        await Assert.That(sections.Select(s => s.Heading).ToList())
            .IsEquivalentTo(new List<string> { "Privatsphäre", "Jugendschutz" });
        await Assert.That(sections[0].HeadingPath)
            .IsEqualTo("Parteiprogramm > Freiheit und Grundrechte > Privatsphäre und Datenschutz > Privatsphäre");
        await Assert.That(sections[1].HeadingPath)
            .IsEqualTo("Parteiprogramm > Freiheit und Grundrechte > Jugendschutz");
    }

    [Test]
    public async Task Strips_links_templates_bold_and_refs() {
        var sections = WikitextSectionSplitter.Split("Parteiprogramm", Sample);

        await Assert.That(sections[0].Body).IsEqualTo("Jedem Bürger muss das Recht auf anonyme Kommunikation garantiert werden.");
        await Assert.That(sections[1].Body).IsEqualTo("Ein wirksamer Jugendschutz kann nicht durch Verbote erreicht werden. Siehe Beispiel.");
    }

    [Test]
    public async Task Anchor_uses_mediawiki_format() {
        var sections = WikitextSectionSplitter.Split("Parteiprogramm", Sample);

        await Assert.That(sections[1].Anchor).IsEqualTo("Jugendschutz");
        await Assert.That(WikitextSectionSplitter.ToAnchor("Privatsphäre und Datenschutz")).IsEqualTo("Privatsphäre_und_Datenschutz");
    }

    [Test]
    public async Task Duplicate_heading_paths_are_disambiguated() {
        var sections = WikitextSectionSplitter.Split("P", "==A==\nx\n==A==\ny");

        await Assert.That(sections.Select(s => s.HeadingPath).ToList())
            .IsEquivalentTo(new List<string> { "P > A", "P > A (2)" });
    }

    [Test]
    public async Task Parent_sections_with_own_text_are_kept() {
        var sections = WikitextSectionSplitter.Split("P", "==A==\nText A\n===B===\nText B");

        await Assert.That(sections.Select(s => s.HeadingPath).ToList())
            .IsEquivalentTo(new List<string> { "P > A", "P > A > B" });
    }

    [Test]
    public async Task Strips_table_markup_from_cell_text() {
        var text = """
            {|
            ! Header 1 !! Header 2
            |-
            | style="color:red" | Cell 1 || Cell 2
            |}
            """;
        var stripped = WikiMarkupStripper.Strip(text);

        await Assert.That(stripped).DoesNotContain("{|");
        await Assert.That(stripped).DoesNotContain("|}");
        await Assert.That(stripped).DoesNotContain("|-");
        await Assert.That(stripped).DoesNotContain("!");
        await Assert.That(stripped).DoesNotContain("|");
        await Assert.That(stripped).Contains("Header 1");
        await Assert.That(stripped).Contains("Header 2");
        await Assert.That(stripped).Contains("Cell 1");
        await Assert.That(stripped).Contains("Cell 2");
    }

    [Test]
    public async Task Keeps_last_segment_of_file_links() {
        var stripped = WikiMarkupStripper.Strip("Siehe [[Datei:Bild.png|300px|Beispielbild]] hier.");

        await Assert.That(stripped).IsEqualTo("Siehe Beispielbild hier.");
    }

    [Test]
    public async Task Keeps_last_segment_of_multi_pipe_links() {
        var stripped = WikiMarkupStripper.Strip("[[Seite|Anker|Text]]");

        await Assert.That(stripped).IsEqualTo("Text");
    }

    [Test]
    public async Task Preserves_non_table_lines_starting_with_pipe_or_exclamation() {
        var stripped = WikiMarkupStripper.Strip("!!! Wichtig !!!\n| kein Tabellenrest");

        await Assert.That(stripped).Contains("Wichtig");
        await Assert.That(stripped).Contains("kein");
        await Assert.That(stripped).Contains("Tabellenrest");
    }
}
