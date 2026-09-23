using Quartermaster.Api.NewsPosts;

namespace Quartermaster.Server.Tests.NewsPosts;

public class PostTextTests {
    [Test]
    public async Task Counts_emoji_and_combined_characters_as_one() {
        await Assert.That(PostText.Length("a👍🏽é")).IsEqualTo(3);
    }

    [Test]
    public async Task Accepts_exactly_the_limit_and_rejects_one_more() {
        var atLimit = new string('x', 279) + "🏴‍☠️";
        await Assert.That(PostText.IsValid(atLimit)).IsTrue();
        await Assert.That(PostText.IsValid(atLimit + "x")).IsFalse();
    }

    [Test]
    public async Task Rejects_blank_text() {
        await Assert.That(PostText.IsValid("   ")).IsFalse();
    }
}
