using Quartermaster.Server.Accreditation;

namespace Quartermaster.Server.Tests.Accreditation;

public class AccreditationUsernameGeneratorTests {
    [Test]
    [Arguments("Erika", "Mustermann", "Erika.Mustermann")]
    [Arguments("  Anna  Lena ", "von der Heide", "Anna.Lena.von.der.Heide")]
    [Arguments("Jörg", "Müller-Lüdenscheidt", "Jörg.Müller-Lüdenscheidt")]
    [Arguments("Dr. Max", "O'Neill (Gast)", "Dr.Max.ONeill.Gast")]
    public async Task BuildBaseName_matches_MemberDataImport(string first, string last, string expected) {
        await Assert.That(AccreditationUsernameGenerator.BuildBaseName(first, last, ".")).IsEqualTo(expected);
    }

    // When the separator itself is a prohibited character, MemberDataImport's join-then-normalize order drops it like any
    // other prohibited character, fusing the two names with no boundary between them; pinned against its real output.
    [Test]
    [Arguments("Erika", "Mustermann", ",", "ErikaMustermann")]
    [Arguments("Anna Lena", "von der Heide", ",", "Anna,Lenavon,der,Heide")]
    [Arguments("Erika", "Mustermann", "/", "ErikaMustermann")]
    [Arguments("Anna Lena", "von der Heide", "/", "Anna/Lenavon/der/Heide")]
    public async Task BuildBaseName_matches_MemberDataImport_for_prohibited_separator(string first, string last, string separator, string expected) {
        await Assert.That(AccreditationUsernameGenerator.BuildBaseName(first, last, separator)).IsEqualTo(expected);
    }

    [Test]
    public async Task BuildBaseName_of_only_prohibited_characters_is_empty() {
        await Assert.That(AccreditationUsernameGenerator.BuildBaseName("$%", "!?", ".")).IsEqualTo("");
    }

    [Test]
    [Arguments("4711")]
    [Arguments(" 4711 ")]
    public async Task HashSuffix_is_six_digits_and_ignores_surrounding_whitespace(string number) {
        var suffix = AccreditationUsernameGenerator.HashSuffix(number);

        await Assert.That(suffix.Length).IsEqualTo(6);
        await Assert.That(suffix.All(char.IsAsciiDigit)).IsTrue();
        await Assert.That(suffix).IsEqualTo(AccreditationUsernameGenerator.HashSuffix("4711"));
    }

    // Literal computed by running MemberDataImport's own UsernameGenerator.HashSuffix("4711"); both generators must agree.
    [Test]
    public async Task HashSuffix_matches_MemberDataImport_reference() {
        await Assert.That(AccreditationUsernameGenerator.HashSuffix("4711")).IsEqualTo("576810");
    }

    [Test]
    [Arguments("Erika.Mustermann", true)]
    [Arguments("Erika Mustermann", false)]
    [Arguments("Erika/Mustermann", false)]
    [Arguments("", false)]
    public async Task IsValid_rejects_whitespace_prohibited_and_empty(string username, bool expected) {
        await Assert.That(AccreditationUsernameGenerator.IsValid(username)).IsEqualTo(expected);
    }
}
