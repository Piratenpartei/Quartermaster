using System.Text.RegularExpressions;
using Quartermaster.Server.Accreditation;

namespace Quartermaster.Server.Tests.Accreditation;

public class ReadablePasswordGeneratorTests {
    [Test]
    public async Task Password_has_three_words_and_two_digits() {
        var password = ReadablePasswordGenerator.Generate();

        await Assert.That(Regex.IsMatch(password, "^[A-Z][a-z]+-[A-Z][a-z]+-[A-Z][a-z]+-[0-9]{2}$")).IsTrue();
        var words = password.Split('-').Take(3);
        await Assert.That(words.All(w => PasswordWords.All.Contains(w))).IsTrue();
    }

    [Test]
    public async Task Word_list_is_large_unique_and_ascii() {
        await Assert.That(PasswordWords.All.Count).IsGreaterThanOrEqualTo(300);
        await Assert.That(PasswordWords.All.Distinct().Count()).IsEqualTo(PasswordWords.All.Count);
        await Assert.That(PasswordWords.All.All(w => Regex.IsMatch(w, "^[A-Z][a-z]{2,7}$"))).IsTrue();
    }

    [Test]
    public async Task Draws_vary() {
        var draws = Enumerable.Range(0, 50).Select(_ => ReadablePasswordGenerator.Generate()).ToHashSet();

        await Assert.That(draws.Count).IsGreaterThan(45);
    }
}
