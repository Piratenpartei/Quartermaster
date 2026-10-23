using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Server.AdministrativeDivisions;

namespace Quartermaster.Server.Tests.AdministrativeDivisions;

public class AdministrativeDivisionNamingTests {
    private static AdministrativeDivisionParseResult Parsed(params (int GeoNameId, string Name, int Depth, string AdminCode)[] divisions) {
        var list = divisions
            .Select(d => new AdministrativeDivision { Name = d.Name, Depth = d.Depth, AdminCode = d.AdminCode })
            .ToList();
        var origins = divisions.ToDictionary(d => d.AdminCode, d => new AdministrativeDivisionOrigin(d.GeoNameId, d.Name));
        return new AdministrativeDivisionParseResult(list, origins);
    }

    private static GeoNamesAlternateName German(int geoNameId, string name, bool preferred = true, bool historic = false) {
        return new GeoNamesAlternateName(geoNameId, "de", name, preferred, false, false, historic);
    }

    private static Gv100AdData Gv100Ad(IReadOnlyList<Gv100AdRegion> regions, IReadOnlyList<Gv100AdCounty> counties) {
        return new Gv100AdData([], [], regions, counties);
    }

    [Test]
    public async Task Sorbian_county_gets_its_preferred_German_name() {
        var parsed = Parsed((3249089, "Wokrejs Sprjewja-Nysa", 6, "12071"));

        var names = AdministrativeDivisionNaming.Apply(parsed, [German(3249089, "Landkreis Spree-Neiße")], Gv100Ad([], [new Gv100AdCounty("12071", "Spree-Neiße")]));

        await Assert.That(parsed.Divisions[0].Name).IsEqualTo("Landkreis Spree-Neiße");
        await Assert.That(names.Select(n => n.Name)).Contains("Wokrejs Sprjewja-Nysa");
        await Assert.That(names.Single(n => n.Source == AdministrativeDivisionNameSource.Gv100Ad).Name).IsEqualTo("Spree-Neiße");
    }

    [Test]
    public async Task Region_takes_the_GV100AD_name_even_without_a_German_alternate() {
        var parsed = Parsed((3214104, "Karlsruhe Region", 5, "82"));

        AdministrativeDivisionNaming.Apply(parsed, [German(3214104, "Karlsruhe", preferred: false)], Gv100Ad([new Gv100AdRegion("082", "Regierungsbezirk Karlsruhe")], []));

        await Assert.That(parsed.Divisions[0].Name).IsEqualTo("Regierungsbezirk Karlsruhe");
    }

    [Test]
    public async Task States_keep_their_short_name() {
        var parsed = Parsed((2911297, "Hamburg", 4, "4"));

        AdministrativeDivisionNaming.Apply(parsed, [German(2911297, "Freie und Hansestadt Hamburg")], null);

        await Assert.That(parsed.Divisions[0].Name).IsEqualTo("Hamburg");
    }

    [Test]
    public async Task Historic_and_non_preferred_names_are_never_shown() {
        var parsed = Parsed((1, "Nuremberg", 7, "9564000"));

        AdministrativeDivisionNaming.Apply(parsed, [German(1, "Nörnberg", historic: true), German(1, "Nürnberg", preferred: false)], null);

        await Assert.That(parsed.Divisions[0].Name).IsEqualTo("Nuremberg");
    }

    [Test]
    public async Task Municipality_is_never_shown_as_its_association() {
        var parsed = Parsed((2949766, "Bersenbrück", 7, "3459006"));

        AdministrativeDivisionNaming.Apply(parsed, [German(2949766, "Samtgemeinde Bersenbrück")], null);

        await Assert.That(parsed.Divisions[0].Name).IsEqualTo("Bersenbrück");
    }
}
