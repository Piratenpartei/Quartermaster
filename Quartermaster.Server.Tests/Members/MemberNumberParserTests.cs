using Quartermaster.Server.Members;

namespace Quartermaster.Server.Tests.Members;

public class MemberNumberParserTests {
    [Test]
    [Arguments("4711", 4711)]
    [Arguments("-4711", -4711)]
    [Arguments(" -12 ", -12)]
    [Arguments("+12", 12)]
    public async Task Parses_positive_and_negative_numbers(string raw, int expected) {
        await Assert.That(MemberNumberParser.TryParse(raw, out var number)).IsTrue();
        await Assert.That(number).IsEqualTo(expected);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("0")]
    [Arguments("abc")]
    [Arguments("--1")]
    [Arguments("1-")]
    [Arguments("1.5")]
    [Arguments("1 000")]
    public async Task Rejects_everything_else(string? raw) {
        await Assert.That(MemberNumberParser.TryParse(raw, out _)).IsFalse();
    }
}
