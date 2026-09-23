using Quartermaster.Server.NewsPosts.Postiz;

namespace Quartermaster.Server.Tests.NewsPosts;

public class PostizRequestBuilderTests {
    private static readonly PostizChannel X = new("int-x", "Piraten X", "x", "@piraten", null, false);
    private static readonly PostizChannel Mastodon = new("int-m", "Piraten Mastodon", "mastodon", "@piraten@social", null, false);

    [Test]
    public async Task Now_post_has_one_entry_per_channel_with_platform_settings() {
        var now = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

        var json = PostizRequestBuilder.Build("Hallo", [X, Mastodon], null, now, "everyone");

        await Assert.That(json["type"]!.GetValue<string>()).IsEqualTo("now");
        await Assert.That(json["shortLink"]!.GetValue<bool>()).IsFalse();
        var posts = json["posts"]!.AsArray();
        await Assert.That(posts.Count).IsEqualTo(2);
        await Assert.That(posts[0]!["integration"]!["id"]!.GetValue<string>()).IsEqualTo("int-x");
        await Assert.That(posts[0]!["value"]![0]!["content"]!.GetValue<string>()).IsEqualTo("Hallo");
        await Assert.That(posts[0]!["settings"]!["__type"]!.GetValue<string>()).IsEqualTo("x");
        await Assert.That(posts[0]!["settings"]!["who_can_reply_post"]!.GetValue<string>()).IsEqualTo("everyone");
        await Assert.That(posts[1]!["settings"]!["__type"]!.GetValue<string>()).IsEqualTo("mastodon");
        await Assert.That(posts[1]!["settings"]!.AsObject().ContainsKey("who_can_reply_post")).IsFalse();
    }

    [Test]
    public async Task Scheduled_post_carries_utc_iso_date() {
        var now = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);
        var at = new DateTime(2026, 9, 24, 8, 30, 0, DateTimeKind.Utc);

        var json = PostizRequestBuilder.Build("Hallo", [Mastodon], at, now, "everyone");

        await Assert.That(json["type"]!.GetValue<string>()).IsEqualTo("schedule");
        await Assert.That(json["date"]!.GetValue<string>()).IsEqualTo("2026-09-24T08:30:00.000Z");
    }
}
