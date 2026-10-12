using System;
using System.Globalization;
using System.Linq;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Quartermaster.Rendering;

public static class MotionWikiConverter {
    private const int MaxDepth = 64;

    private static readonly string[] MagicLinkKeywords = ["ISBN", "RFC", "PMID"];

    private static readonly string[] BlockTags = [
        "p", "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "table", "tr", "th", "td"
    ];

    private static readonly string[] InlineTags = ["strong", "em", "del", "s", "code"];

    // thead/tbody/tfoot are not in the MediaWiki 1.35 HTML whitelist (believed to be shown as literal text, unverified)
    private static readonly string[] StructuralTags = [
        "ul", "ol", "table", "thead", "tbody", "tfoot", "tr", "blockquote", "body"
    ];

    /// <summary>
    /// Converts restricted motion Markdown into wikitext that is safe inside a template parameter. Null or empty input yields an empty string.
    /// </summary>
    public static string ToWikitext(string? markdown) {
        if (string.IsNullOrEmpty(markdown))
            return "";

        var html = MarkdownService.ToHtml(TextNormalization.ToNfc(markdown), SanitizationProfile.Standard, MarkdownFlavor.Motion);
        return ConvertHtml(html);
    }

    internal static string ConvertHtml(string html) {
        if (string.IsNullOrEmpty(html))
            return "";

        var document = new HtmlParser().ParseDocument(html);
        var sb = new StringBuilder(html.Length);
        if (document.Body != null)
            WriteChildren(sb, document.Body, 0);

        return DefuseMagicLinks(sb.ToString().Trim());
    }

    /// <summary>
    /// Converts plain single-line text (title, keywords) into wikitext. Line breaks never survive. Null yields an empty string.
    /// </summary>
    public static string EscapeInline(string? text) {
        if (string.IsNullOrEmpty(text))
            return "";

        var sb = new StringBuilder(text.Length);
        AppendEscaped(sb, text, false);
        var result = sb.ToString().Trim();
        if (result.StartsWith('-'))
            result = "&#45;" + result[1..];

        return DefuseMagicLinks(result);
    }

    private static void WriteChildren(StringBuilder sb, INode parent, int depth) {
        foreach (var child in parent.ChildNodes) {
            WriteNode(sb, child, depth);
        }
    }

    private static void WriteNode(StringBuilder sb, INode node, int depth) {
        if (node is IText text) {
            WriteText(sb, text);
            return;
        }

        if (node is not IElement element)
            return;

        var name = element.LocalName;
        if (depth >= MaxDepth) {
            AppendEscaped(sb, element.TextContent, false);
            return;
        }

        switch (name) {
            case "br":
                sb.Append("<br />");
                break;
            case "hr":
                StartBlock(sb);
                sb.Append("<hr />");
                break;
            case "pre":
                StartBlock(sb);
                sb.Append("<pre>");
                AppendEscaped(sb, element.TextContent.TrimEnd('\n'), true);
                sb.Append("</pre>");
                break;
            case "a":
                WriteLink(sb, element, depth);
                break;
            default:
                WriteElement(sb, element, name, depth);
                break;
        }
    }

    private static void WriteText(StringBuilder sb, IText text) {
        if (IsIgnorableWhitespace(text))
            return;

        var data = text.Data;
        if (text.PreviousSibling is IElement previous && previous.LocalName == "br")
            data = data.TrimStart();

        AppendEscaped(sb, data, false);
    }

    private static void StartBlock(StringBuilder sb) {
        TrimTrailingSpaces(sb);
        sb.Append('\n');
    }

    private static void TrimTrailingSpaces(StringBuilder sb) {
        while (sb.Length > 0 && sb[^1] == ' ') {
            sb.Length--;
        }
    }

    private static void WriteElement(StringBuilder sb, IElement element, string name, int depth) {
        if (BlockTags.Contains(name)) {
            StartBlock(sb);
            sb.Append('<').Append(name).Append('>');
            WriteChildren(sb, element, depth + 1);
            TrimTrailingSpaces(sb);
            sb.Append("</").Append(name).Append('>');
        } else if (InlineTags.Contains(name)) {
            sb.Append('<').Append(name).Append('>');
            WriteChildren(sb, element, depth + 1);
            sb.Append("</").Append(name).Append('>');
        } else {
            WriteChildren(sb, element, depth + 1);
        }
    }

    private static void WriteLink(StringBuilder sb, IElement element, int depth) {
        var href = element.GetAttribute("href")?.Trim();
        if (!IsAllowedLinkTarget(href)) {
            WriteChildren(sb, element, depth + 1);
            return;
        }

        sb.Append('[');
        AppendUrl(sb, href!);
        sb.Append(' ');
        WriteChildren(sb, element, depth + 1);
        sb.Append(']');
    }

    private static bool IsAllowedLinkTarget(string? href) {
        if (string.IsNullOrEmpty(href))
            return false;
        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return true;
        if (href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return true;

        return href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnorableWhitespace(IText text) {
        if (!string.IsNullOrWhiteSpace(text.Data))
            return false;

        return text.ParentElement != null && StructuralTags.Contains(text.ParentElement.LocalName);
    }

    private static void AppendUrl(StringBuilder sb, string rawUrl) {
        var url = TextNormalization.ToNfc(rawUrl);
        for (var i = 0; i < url.Length; i++) {
            var c = url[i];
            switch (c) {
                case '[':
                case ']':
                case '|':
                case '{':
                case '}':
                case '<':
                case '>':
                case '"':
                case '\'':
                case '\\':
                case '`':
                case '~':
                    AppendPercentEncoded(sb, c);
                    break;
                case '_':
                    // __TOC__-style magic words are matched against the whole text, URLs included
                    if (i + 1 < url.Length && url[i + 1] == '_') {
                        AppendPercentEncoded(sb, c);
                    } else {
                        sb.Append(c);
                    }
                    break;
                default:
                    if (char.IsWhiteSpace(c) || char.IsControl(c) || IsInvisible(c)) {
                        AppendPercentEncoded(sb, c);
                    } else {
                        sb.Append(c);
                    }
                    break;
            }
        }
    }

    private static bool IsInvisible(char c) {
        var category = char.GetUnicodeCategory(c);
        return category == UnicodeCategory.Format || category == UnicodeCategory.SpaceSeparator;
    }

    private static void AppendPercentEncoded(StringBuilder sb, char c) {
        foreach (var value in Encoding.UTF8.GetBytes(c.ToString())) {
            sb.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
        }
    }

    private static void AppendEscaped(StringBuilder sb, string rawText, bool preformatted) {
        // MediaWiki stores text in NFC, so the output must already be NFC-stable
        var text = TextNormalization.ToNfc(rawText);
        for (var i = 0; i < text.Length; i++) {
            var combiningLength = CombiningMarkLength(text, i);
            if (combiningLength > 0) {
                AppendCombiningMark(sb, text, i, combiningLength);
                i += combiningLength - 1;
                continue;
            }

            var c = text[i];
            switch (c) {
                case '&':
                    sb.Append("&amp;");
                    break;
                case '|':
                    // {{!}} turns into a live pipe after template expansion (cell attributes, extra cells); the entity never does
                    AppendCharacterReference(sb, c);
                    break;
                case '{':
                case '}':
                case '[':
                case ']':
                case '<':
                case '>':
                case '~':
                case '\'':
                case '_':
                case ':':
                case '!':
                case '=':
                case '*':
                case '#':
                case ';':
                    // { } open templates/parameters, [ ] links and categories, < > tags and comments, ~ signatures,
                    // ' bold/italic, _ magic words, : protocols (bare URLs become links) and namespaces,
                    // ! = * # ; list/table/heading markers and attribute syntax once a template result lands at a line start
                    AppendCharacterReference(sb, c);
                    break;
                case ' ':
                case '-':
                    // a space or ---- at the start of a line inside <pre> is not parsed, but is kept out of line-start syntax anyway
                    if (preformatted && sb.Length > 0 && sb[^1] == '\n') {
                        AppendCharacterReference(sb, c);
                    } else {
                        sb.Append(c);
                    }
                    break;
                case '"':
                    sb.Append("&quot;");
                    break;
                case '\n':
                case '\t':
                    sb.Append(preformatted ? c : ' ');
                    break;
                default:
                    if (char.IsWhiteSpace(c)) {
                        sb.Append(' ');
                    } else if (!char.IsControl(c)) {
                        sb.Append(c);
                    }
                    break;
            }
        }
    }

    // Runs over the finished string so that keywords assembled across text nodes, dropped elements or removed
    // characters are caught. Only text between our own tags is touched; link URLs are left alone.
    private static string DefuseMagicLinks(string wikitext) {
        var sb = new StringBuilder(wikitext.Length);
        var inTag = false;
        var inUrl = false;
        for (var i = 0; i < wikitext.Length; i++) {
            var c = wikitext[i];
            if (c == '<') {
                inTag = true;
            } else if (c == '>') {
                inTag = false;
            } else if (c == '[' && !inTag) {
                inUrl = true;
            } else if (c == ' ') {
                inUrl = false;
            }

            if (!inTag && !inUrl && StartsMagicLinkWord(wikitext, i)) {
                AppendCharacterReference(sb, c);
            } else {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static bool StartsMagicLinkWord(string text, int index) {
        if (index > 0 && char.IsAsciiLetterOrDigit(text[index - 1]))
            return false;

        foreach (var keyword in MagicLinkKeywords) {
            if (IsWordAt(text, index, keyword))
                return true;
        }

        return false;
    }

    private static bool IsWordAt(string text, int index, string word) {
        if (!text.AsSpan(index).StartsWith(word, StringComparison.Ordinal))
            return false;

        var end = index + word.Length;
        return end == text.Length || !char.IsAsciiLetterOrDigit(text[end]);
    }

    private static int CombiningMarkLength(string text, int index) {
        var length = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
        var category = CharUnicodeInfo.GetUnicodeCategory(text, index);
        if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark || category == UnicodeCategory.EnclosingMark)
            return length;

        return 0;
    }

    // A combining mark directly after a character we emit (the > of a tag, the ; of an entity, a space, the = before the value)
    // would compose with it under NFC (for example > + U+0338), so it is only written raw after a letter or digit from the member text.
    private static void AppendCombiningMark(StringBuilder sb, string text, int index, int length) {
        if (sb.Length > 0 && char.IsAsciiLetterOrDigit(sb[^1])) {
            sb.Append(text, index, length);
            return;
        }

        sb.Append("&#x").Append(char.ConvertToUtf32(text, index).ToString("X", CultureInfo.InvariantCulture)).Append(';');
    }

    private static void AppendCharacterReference(StringBuilder sb, char c) {
        sb.Append("&#").Append(((int)c).ToString(CultureInfo.InvariantCulture)).Append(';');
    }
}
