using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Server.Tests.AdministrativeDivisions;

public class Gv100AdParserTests {
    private static Gv100AdData ParseSample() {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "gv100ad_sample.txt");
        using var reader = new StreamReader(path);
        return Gv100AdParser.Parse(reader);
    }

    [Test]
    public async Task Parses_associations_and_municipalities() {
        var data = ParseSample();

        var lachendorf = data.Municipalities.Single(m => m.Ags == "03351016");
        var samtgemeinde = data.Associations.Single(a => a.Key == lachendorf.AssociationKey);
        var celle = data.Municipalities.Single(m => m.Ags == "03351006");
        var celleAssociation = data.Associations.Single(a => a.Key == celle.AssociationKey);

        await Assert.That(data.Municipalities.Count).IsEqualTo(5);
        await Assert.That(data.Associations.Count).IsEqualTo(4);
        await Assert.That(lachendorf.Name).IsEqualTo("Lachendorf");
        await Assert.That(lachendorf.AssociationKey).IsEqualTo("033515403");
        await Assert.That(data.Municipalities.Single(m => m.Ags == "03351002").AssociationKey).IsEqualTo("033515403");
        await Assert.That(samtgemeinde.Name).IsEqualTo("Lachendorf");
        await Assert.That(samtgemeinde.Kind).IsEqualTo(52);
        await Assert.That(Gv100AdParser.CertifiesForMembers(samtgemeinde.Kind)).IsTrue();
        await Assert.That(celleAssociation.Kind).IsEqualTo(50);
        await Assert.That(Gv100AdParser.CertifiesForMembers(celleAssociation.Kind)).IsFalse();
    }

    [Test]
    public async Task Names_with_umlauts_keep_the_following_columns() {
        var data = ParseSample();

        await Assert.That(data.Municipalities.Single(m => m.Ags == "03351012").Name).IsEqualTo("Hambühren");
        await Assert.That(data.Associations.Single(a => a.Key == "010030000").Name).IsEqualTo("Lübeck, Hansestadt");
        await Assert.That(data.Associations.Single(a => a.Key == "010030000").Kind).IsEqualTo(50);
    }

    [Test]
    public async Task Short_or_unknown_lines_are_skipped() {
        using var reader = new StringReader("60\n10202609300\n\n99xxxxxxxxxxxxxxxxxxxxxxxxxx\n");

        var data = Gv100AdParser.Parse(reader);

        await Assert.That(data.Municipalities.Count).IsEqualTo(0);
        await Assert.That(data.Associations.Count).IsEqualTo(0);
    }
}
