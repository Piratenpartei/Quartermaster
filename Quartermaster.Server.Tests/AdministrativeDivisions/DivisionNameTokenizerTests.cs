using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Server.AdministrativeDivisions;

namespace Quartermaster.Server.Tests.AdministrativeDivisions;

public class DivisionNameTokenizerTests {
    [Test]
    public async Task Designators_are_split_from_the_name() {
        var tokens = DivisionNameTokenizer.Tokenize("Landkreis Spree-Neiße");

        await Assert.That(tokens.Core).IsEquivalentTo(["spree", "neisse"]);
        await Assert.That(tokens.Designators).IsEquivalentTo(["landkreis"]);
        await Assert.That(tokens.Joined).IsEqualTo("spreeneisse");
    }

    [Test]
    [Arguments("Göttingen", "gottingen")]
    [Arguments("Goettingen", "gottingen")]
    [Arguments("Bautzen/Budyšin", "bautzen budysin")]
    [Arguments("Kassel, documenta-Stadt", "kassel documenta stadt")]
    public async Task Normalize_drops_umlauts_accents_and_punctuation(string text, string expected) {
        await Assert.That(string.Join(' ', DivisionNameTokenizer.Words(text))).IsEqualTo(expected);
    }

    [Test]
    public async Task City_district_words_are_designators() {
        var tokens = DivisionNameTokenizer.Tokenize("Stadtbezirk Neustadt");

        await Assert.That(tokens.Core).IsEquivalentTo(["neustadt"]);
        await Assert.That(tokens.Designators).IsEquivalentTo(["stadtbezirk"]);
    }

    [Test]
    public async Task A_name_of_designators_only_keeps_them_as_its_name() {
        var tokens = DivisionNameTokenizer.Tokenize("Stadt");

        await Assert.That(tokens.Core).IsEquivalentTo(["stadt"]);
        await Assert.That(tokens.Designators).IsEmpty();
    }
}

public class EditDistanceTests {
    [Test]
    [Arguments("neisse", "neisse", 0)]
    [Arguments("neise", "neisse", 1)]
    [Arguments("nesise", "neisse", 1)]
    [Arguments("gottingen", "gotingen", 1)]
    [Arguments("hildesheim", "hildeshiem", 1)]
    public async Task Counts_inserts_deletes_replacements_and_swaps(string a, string b, int expected) {
        await Assert.That(EditDistance.Bounded(a, b, 2)).IsEqualTo(expected);
    }

    [Test]
    public async Task Stops_above_the_bound() {
        await Assert.That(EditDistance.Bounded("hannover", "berlin", 2)).IsEqualTo(3);
    }
}

public class GeoNamesAlternateNameParserTests {
    [Test]
    public async Task Keeps_latin_names_of_known_divisions_only() {
        using var reader = new StringReader(string.Join("\n",
            "1\t42\tde\tLandkreis Spree-Neiße\t1\t\t\t\t\t",
            "2\t42\thsb\tWokrejs Sprjewja-Nysa\t\t\t\t\t\t",
            "3\t42\tlink\thttps://en.wikipedia.org/wiki/Spree-Nei%C3%9Fe\t\t\t\t\t\t",
            "4\t42\tru\tШпре-Найсе\t\t\t\t\t\t",
            "5\t7\tde\tSomewhere else\t\t\t\t\t\t",
            "short line"));

        var names = GeoNamesAlternateNameParser.Parse(reader, new HashSet<int> { 42 });

        await Assert.That(names.Select(n => n.Name)).IsEquivalentTo(["Landkreis Spree-Neiße", "Wokrejs Sprjewja-Nysa"]);
        await Assert.That(names[0].IsPreferred).IsTrue();
        await Assert.That(names[1].Language).IsEqualTo("hsb");
    }
}
