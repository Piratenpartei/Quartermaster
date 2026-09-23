using Quartermaster.Server.NewsPosts.Feeds;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class FeedParserTests {
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Rss_uses_content_encoded_as_plain_article_text() {
        var items = RssFeedParser.Parse(FakeFeedFetcher.Load("netzpolitik.xml"), Now);

        await Assert.That(items[0].ExternalId).IsEqualTo("https://netzpolitik.org/?p=1");
        await Assert.That(items[0].Teaser).IsEqualTo("Die Kommission plant eine Alterskontrolle.");
        await Assert.That(items[0].ArticleText).IsEqualTo("Ein elfseitiges Papier.\n\nZweiter Absatz.");
        await Assert.That(items[0].PublishedAt).IsEqualTo(new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task Rss_item_without_guid_and_date_falls_back_to_link_and_now() {
        var items = RssFeedParser.Parse(FakeFeedFetcher.Load("netzpolitik.xml"), Now);

        await Assert.That(items[1].ExternalId).IsEqualTo("https://netzpolitik.org/2026/ohne/");
        await Assert.That(items[1].PublishedAt).IsEqualTo(Now);
        await Assert.That(items[1].ArticleText).IsNull();
    }

    [Test]
    public async Task Atom_entries_are_parsed() {
        var items = RssFeedParser.Parse(FakeFeedFetcher.Load("atom.xml"), Now);

        await Assert.That(items.Single().Url).IsEqualTo("https://example.org/atom-1");
        await Assert.That(items.Single().Teaser).IsEqualTo("Atom Teaser");
    }

    [Test]
    public async Task Tagesschau_list_keeps_stories_only() {
        var items = TagesschauParser.ParseList(FakeFeedFetcher.Load("tagesschau-news.json"), Now);

        await Assert.That(items.Select(i => i.ExternalId).ToList()).IsEquivalentTo(new List<string> { "ts-1", "ts-2" });
        await Assert.That(items[0].Url).IsEqualTo("https://www.tagesschau.de/inland/tickets-100.html");
        await Assert.That(items[0].DetailsUrl).IsEqualTo("https://www.tagesschau.de/api2u/inland/tickets-100.json");
        await Assert.That(items[1].Teaser).IsEqualTo("");
    }

    [Test]
    public async Task Tagesschau_details_joins_text_and_headlines_as_plain_text() {
        var text = TagesschauParser.ParseDetails(FakeFeedFetcher.Load("tagesschau-details.json"));

        await Assert.That(text).IsEqualTo("Einzelfahrschein 4 Euro.\n\nZwischentitel\n\nDer Senat verschiebt die Reform.");
    }

    [Test]
    public async Task Rss_items_with_a_non_http_url_are_dropped() {
        var xml = """
            <rss version="2.0"><channel>
            <item><guid>1</guid><link>javascript:alert(1)</link><title>Bad</title></item>
            <item><guid>2</guid><link>https://example.org/ok</link><title>Ok</title></item>
            </channel></rss>
            """;

        var items = RssFeedParser.Parse(xml, Now);

        await Assert.That(items.Select(i => i.Url).ToList()).IsEquivalentTo(new List<string> { "https://example.org/ok" });
    }

    [Test]
    public async Task Tagesschau_items_with_a_non_http_share_url_are_dropped() {
        const string json = """
            {"news":[
                {"type":"story","externalId":"a","shareURL":"javascript:alert(1)","title":"Bad"},
                {"type":"story","externalId":"b","shareURL":"https://example.org/ok","title":"Ok"}
            ]}
            """;

        var items = TagesschauParser.ParseList(json, Now);

        await Assert.That(items.Select(i => i.ExternalId).ToList()).IsEquivalentTo(new List<string> { "b" });
    }

    [Test]
    public async Task Malformed_xml_throws_FormatException() {
        await Assert.ThrowsAsync<FormatException>(() => Task.FromResult(RssFeedParser.Parse("<html>nope", Now)));
    }

    [Test]
    public async Task Xml_with_doctype_entity_expansion_throws_FormatException() {
        var xml = "<?xml version=\"1.0\"?><!DOCTYPE rss [<!ENTITY a \"aaaa\">]><rss version=\"2.0\"><channel></channel></rss>";

        await Assert.ThrowsAsync<FormatException>(() => Task.FromResult(RssFeedParser.Parse(xml, Now)));
    }
}
