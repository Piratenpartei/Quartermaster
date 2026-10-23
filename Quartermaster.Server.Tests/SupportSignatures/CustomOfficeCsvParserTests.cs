using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.I18n;
using Quartermaster.Server.SupportSignatures;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class CustomOfficeCsvParserTests {
    private const string Header = "Gemeinde,Gemeindeschluessel,Amt,Zusatz,Strasse_HausNr,PLZ,Ort,Bezeichnung_Liste_2024\n";

    [Test]
    public async Task Reads_the_columns_by_header() {
        var csv = CustomOfficeCsvParser.Parse(Header + "Salzgitter,03102000,Stadt Salzgitter,Wahlbüro,Joachim-Campe-Straße 6-8,38226,Salzgitter,Salzgitter\n");

        var row = csv.Rows.Single();
        await Assert.That(csv.ErrorKey).IsNull();
        await Assert.That(row.MunicipalityKey).IsEqualTo("03102000");
        await Assert.That(row.Name).IsEqualTo("Stadt Salzgitter");
        await Assert.That(row.Addition).IsEqualTo("Wahlbüro");
        await Assert.That(row.Street).IsEqualTo("Joachim-Campe-Straße 6-8");
        await Assert.That(row.PostCode).IsEqualTo("38226");
        await Assert.That(row.City).IsEqualTo("Salzgitter");
    }

    [Test]
    public async Task Pads_keys_excel_shortened_and_reads_semicolons() {
        var csv = CustomOfficeCsvParser.Parse("Ort;PLZ;Strasse_HausNr;Amt;gemeindeschluessel\nWolfsburg;38440;Porschestraße 49;Stadt Wolfsburg;3103000\n");

        await Assert.That(csv.Rows.Single().MunicipalityKey).IsEqualTo("03103000");
        await Assert.That(csv.Rows.Single().Addition).IsNull();
    }

    [Test]
    public async Task Missing_column_refuses_the_file() {
        var csv = CustomOfficeCsvParser.Parse("Gemeinde,Gemeindeschluessel,Amt,PLZ,Ort\nWolfsburg,03103000,Stadt Wolfsburg,38440,Wolfsburg\n");

        await Assert.That(csv.ErrorKey).IsEqualTo(I18nKey.Error.SupportSignatures.OfficeImport.MissingColumns);
    }

    [Test]
    public async Task Header_only_is_empty() {
        await Assert.That(CustomOfficeCsvParser.Parse(Header).ErrorKey).IsEqualTo(I18nKey.Error.SupportSignatures.OfficeImport.Empty);
        await Assert.That(CustomOfficeCsvParser.Parse("").ErrorKey).IsEqualTo(I18nKey.Error.SupportSignatures.OfficeImport.Empty);
    }

    [Test]
    public async Task Faulty_rows_are_reported_with_their_line() {
        var csv = CustomOfficeCsvParser.Parse(Header +
            "Wolfsburg,03103000,Stadt Wolfsburg,,Porschestraße 49,38440,Wolfsburg,\n" +
            "Wolfsburg,03103000,Stadt Wolfsburg,,Porschestraße 49,38440,Wolfsburg,\n" +
            "Irgendwo,ABC,Stadt X,,Weg 1,12345,X,\n" +
            "Salzgitter,03102000,Stadt Salzgitter,,,38226,Salzgitter,\n");

        await Assert.That(csv.RowCount).IsEqualTo(4);
        await Assert.That(csv.Rows.Count).IsEqualTo(1);
        await Assert.That(csv.Problems.Select(p => (p.Line, p.ErrorKey)).ToList()).IsEquivalentTo([
            (3, I18nKey.Error.SupportSignatures.OfficeImport.DuplicateKey),
            (4, I18nKey.Error.SupportSignatures.OfficeImport.InvalidKey),
            (5, I18nKey.Error.SupportSignatures.OfficeImport.MissingField)
        ]);
    }
}
