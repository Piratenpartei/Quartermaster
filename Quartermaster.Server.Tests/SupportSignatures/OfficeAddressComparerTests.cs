using System.Text;
using System.Threading.Tasks;
using Quartermaster.Server.SupportSignatures;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class OfficeAddressComparerTests {
    [Test]
    [Arguments("Hauptstr. 30", "Hauptstraße 30")]
    [Arguments("Hauptstrasse 30", "HAUPTSTRASSE  30")]
    [Arguments("Joachim-Campe-Straße 6-8", "Joachim-Campe-Straße 6 - 8")]
    public async Task Spellings_of_the_same_street_match(string a, string b) {
        await Assert.That(OfficeAddressComparer.SameAddress(a, "38226", "Salzgitter", b, "38226", "Salzgitter")).IsTrue();
    }

    [Test]
    public async Task Different_house_number_or_postcode_does_not_match() {
        await Assert.That(OfficeAddressComparer.SameAddress("Reichsstraße 3", "38100", "Braunschweig", "Reichsstraße 4", "38100", "Braunschweig")).IsFalse();
        await Assert.That(OfficeAddressComparer.SameAddress("Reichsstraße 3", "38100", "Braunschweig", "Reichsstraße 3", "38102", "Braunschweig")).IsFalse();
    }
}

public class CsvTextDecoderTests {
    [Test]
    public async Task Reads_utf8_with_and_without_bom() {
        var bytes = Encoding.UTF8.GetBytes("Reichsstraße");

        await Assert.That(CsvTextDecoder.Decode(bytes)).IsEqualTo("Reichsstraße");
        await Assert.That(CsvTextDecoder.Decode([.. Encoding.UTF8.Preamble, .. bytes])).IsEqualTo("Reichsstraße");
    }

    [Test]
    public async Task Falls_back_to_the_excel_encoding() {
        var bytes = CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetBytes("Reichsstraße „Wahlamt“");

        await Assert.That(CsvTextDecoder.Decode(bytes)).IsEqualTo("Reichsstraße „Wahlamt“");
    }
}
