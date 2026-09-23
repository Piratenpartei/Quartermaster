using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Quartermaster.Server.NewsPosts.Feeds;

public static class TagesschauParser {
    public static List<ParsedFeedItem> ParseList(string json, DateTime nowUtc) {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("news")
            .EnumerateArray()
            .Where(n => Str(n, "type") == "story")
            .Select(n => new ParsedFeedItem(
                Str(n, "externalId") ?? Str(n, "shareURL") ?? "",
                Str(n, "shareURL") ?? "",
                Str(n, "title") ?? "",
                Str(n, "firstSentence") ?? "",
                null,
                DateTimeOffset.TryParse(Str(n, "date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date.UtcDateTime : nowUtc,
                Str(n, "details")))
            .Where(i => FeedUrl.IsHttp(i.Url))
            .ToList();
    }

    public static string ParseDetails(string json) {
        using var doc = JsonDocument.Parse(json);
        var parts = doc.RootElement.GetProperty("content")
            .EnumerateArray()
            .Where(c => Str(c, "type") == "text" || Str(c, "type") == "headline")
            .Select(c => HtmlText.ToPlain(Str(c, "value") ?? ""))
            .Where(t => t.Length > 0);
        return string.Join("\n\n", parts);
    }

    private static string? Str(JsonElement element, string name) {
        if (!element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
