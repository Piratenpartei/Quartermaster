using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Quartermaster.Server.NewsPosts.Feeds;

public static class RssFeedParser {
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    // Feeds are untrusted input: DTDs must stay prohibited (blocks entity-expansion / billion-laughs
    // payloads) and the resolver disabled (blocks fetching external DTDs/entities).
    private static readonly XmlReaderSettings ReaderSettings = new() {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    public static List<ParsedFeedItem> Parse(string xml, DateTime nowUtc) {
        XDocument doc;
        try {
            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, ReaderSettings);
            doc = XDocument.Load(xmlReader);
        } catch (XmlException ex) {
            throw new FormatException("Feed is not valid XML: " + ex.Message, ex);
        }

        var atomEntries = doc.Descendants(Atom + "entry").ToList();
        if (atomEntries.Count > 0)
            return atomEntries.Select(e => ParseAtomEntry(e, nowUtc)).Where(i => FeedUrl.IsHttp(i.Url)).ToList();

        return doc.Descendants("item").Select(e => ParseRssItem(e, nowUtc)).Where(i => FeedUrl.IsHttp(i.Url)).ToList();
    }

    private static ParsedFeedItem ParseRssItem(XElement item, DateTime nowUtc) {
        var link = item.Element("link")?.Value.Trim() ?? "";
        var guid = item.Element("guid")?.Value.Trim();
        var encoded = item.Element(Content + "encoded")?.Value;
        return new ParsedFeedItem(
            string.IsNullOrEmpty(guid) ? link : guid,
            link,
            HtmlText.ToPlain(item.Element("title")?.Value ?? ""),
            HtmlText.ToPlain(item.Element("description")?.Value ?? ""),
            string.IsNullOrWhiteSpace(encoded) ? null : HtmlText.ToPlain(encoded),
            ParseDate(item.Element("pubDate")?.Value, nowUtc),
            null);
    }

    private static ParsedFeedItem ParseAtomEntry(XElement entry, DateTime nowUtc) {
        var link = entry.Elements(Atom + "link")
            .Where(l => (string?)l.Attribute("rel") == null || (string?)l.Attribute("rel") == "alternate")
            .Select(l => (string?)l.Attribute("href"))
            .FirstOrDefault() ?? "";
        var id = entry.Element(Atom + "id")?.Value.Trim();
        var content = entry.Element(Atom + "content")?.Value;
        return new ParsedFeedItem(
            string.IsNullOrEmpty(id) ? link : id,
            link,
            HtmlText.ToPlain(entry.Element(Atom + "title")?.Value ?? ""),
            HtmlText.ToPlain(entry.Element(Atom + "summary")?.Value ?? ""),
            string.IsNullOrWhiteSpace(content) ? null : HtmlText.ToPlain(content),
            ParseDate(entry.Element(Atom + "updated")?.Value ?? entry.Element(Atom + "published")?.Value, nowUtc),
            null);
    }

    private static DateTime ParseDate(string? raw, DateTime nowUtc) {
        if (string.IsNullOrWhiteSpace(raw))
            return nowUtc;
        if (DateTimeOffset.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.UtcDateTime;
        return nowUtc;
    }
}
