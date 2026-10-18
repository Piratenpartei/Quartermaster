using System.Threading.Tasks;
using Quartermaster.Api.Banking;

namespace Quartermaster.Server.Tests.Banking;

public class IbanTests {
    [Test]
    [Arguments("DE89370400440532013000")]
    [Arguments("de89 3704 0044 0532 0130 00")]
    [Arguments("GB82WEST12345698765432")]
    [Arguments("AT611904300234573201")]
    [Arguments("NL91ABNA0417164300")]
    [Arguments("CH9300762011623852957")]
    public async Task Accepts_valid_ibans(string iban) {
        await Assert.That(Iban.IsValid(iban)).IsTrue();
    }

    [Test]
    [Arguments("DE89370400440532013001")]
    [Arguments("DE98370400440532013000")]
    [Arguments("DE8937040044053201300")]
    [Arguments("DE89-3704-0044-0532-0130-00")]
    [Arguments("8937040044053201300012")]
    [Arguments("DE89")]
    [Arguments("")]
    [Arguments(null)]
    public async Task Rejects_invalid_ibans(string? iban) {
        await Assert.That(Iban.IsValid(iban)).IsFalse();
    }

    [Test]
    public async Task Normalize_removes_spaces_and_uppercases() {
        await Assert.That(Iban.Normalize(" de89 3704\t0044 ")).IsEqualTo("DE8937040044");
    }
}
