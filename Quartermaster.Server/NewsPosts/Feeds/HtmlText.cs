using System.Net;
using System.Text.RegularExpressions;

namespace Quartermaster.Server.NewsPosts.Feeds;

public static partial class HtmlText {
    /// <summary>Turns article HTML into plain text: block ends become paragraph breaks, tags are dropped, entities decoded.</summary>
    public static string ToPlain(string html) {
        var text = BlockEndRegex().Replace(html, "\n\n");
        text = BreakRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, "");
        text = WebUtility.HtmlDecode(text);
        text = InlineWhitespaceRegex().Replace(text, " ");
        text = SpaceAroundNewlineRegex().Replace(text, "\n");
        text = ManyNewlinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"</(p|h[1-6]|li|div|blockquote)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t\r\f\v]+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@" *\n *")]
    private static partial Regex SpaceAroundNewlineRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyNewlinesRegex();
}
