using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Quartermaster.Server.NewsPosts.Programme;

public static partial class WikiMarkupStripper {
    public static string Strip(string wikitext) {
        var text = RefRegex().Replace(wikitext, "");
        text = CommentRegex().Replace(text, "");
        text = RemoveTemplates(text);
        text = TagRegex().Replace(text, "");
        text = StripTables(text);
        text = ProcessWikiLinks(text);
        text = ExternalLinkWithLabelRegex().Replace(text, "$1");
        text = ExternalLinkBareRegex().Replace(text, "");
        text = text.Replace("'''", "").Replace("''", "");
        text = ListMarkerRegex().Replace(text, "");
        text = WhitespaceRegex().Replace(text, " ");
        return SpaceBeforePunctuationRegex().Replace(text, "$1").Trim();
    }

    // Templates nest ({{a|{{b}}}}) so a single regex cannot match; remove innermost-first until none remain.
    private static string RemoveTemplates(string text) {
        string previous;
        do {
            previous = text;
            text = InnermostTemplateRegex().Replace(text, "");
        } while (text != previous);
        return text;
    }

    private static string StripTables(string text) {
        var lines = text.Split('\n');
        var result = new StringBuilder();
        var tableDepth = 0;

        foreach (var line in lines) {
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("{|")) {
                tableDepth++;
                continue;
            }

            if (trimmed.StartsWith("|}")) {
                if (tableDepth > 0) {
                    tableDepth--;
                }
                continue;
            }

            if (tableDepth == 0) {
                result.AppendLine(line);
                continue;
            }

            if (trimmed.StartsWith("|-")) {
                continue;
            }

            if (!trimmed.StartsWith("|") && !trimmed.StartsWith("!")) {
                result.AppendLine(line);
                continue;
            }

            var content = trimmed.Substring(1);
            var cells = CellSplitRegex().Split(content);
            var cellTexts = new List<string>();

            foreach (var cell in cells) {
                var cellText = ExtractCellContent(cell);
                if (!string.IsNullOrWhiteSpace(cellText)) {
                    cellTexts.Add(cellText);
                }
            }

            if (cellTexts.Count > 0) {
                result.AppendLine(string.Join(" ", cellTexts));
            }
        }

        return result.ToString();
    }

    private static string ExtractCellContent(string cell) {
        var bracketDepth = 0;
        var lastSinglePipeIndex = -1;

        for (var i = 0; i < cell.Length; i++) {
            if (i + 1 < cell.Length && cell[i] == '[' && cell[i + 1] == '[') {
                bracketDepth++;
                i++;
                continue;
            }

            if (i + 1 < cell.Length && cell[i] == ']' && cell[i + 1] == ']') {
                bracketDepth--;
                i++;
                continue;
            }

            if (cell[i] == '|' && bracketDepth == 0) {
                var isPrevPipe = i > 0 && cell[i - 1] == '|';
                var isNextPipe = i + 1 < cell.Length && cell[i + 1] == '|';

                if (!isPrevPipe && !isNextPipe) {
                    lastSinglePipeIndex = i;
                }
            }
        }

        if (lastSinglePipeIndex >= 0) {
            return cell.Substring(lastSinglePipeIndex + 1).Trim();
        }

        return cell.Trim();
    }

    private static string ProcessWikiLinks(string text) {
        return WikiLinkRegex().Replace(text, match => {
            var content = match.Groups[1].Value;
            var segments = content.Split('|');
            return segments[segments.Length - 1];
        });
    }

    [GeneratedRegex(@"<ref[^>]*/>|<ref[^>]*>.*?</ref>", RegexOptions.Singleline)]
    private static partial Regex RefRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\{\{[^{}]*\}\}")]
    private static partial Regex InnermostTemplateRegex();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\[\[([^\]]*)\]\]")]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(@"\[https?://\S+\s+([^\]]+)\]")]
    private static partial Regex ExternalLinkWithLabelRegex();

    [GeneratedRegex(@"\[https?://[^\]\s]+\]")]
    private static partial Regex ExternalLinkBareRegex();

    [GeneratedRegex(@"^[*#:;]+\s*", RegexOptions.Multiline)]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s+([.,;:!?])")]
    private static partial Regex SpaceBeforePunctuationRegex();

    [GeneratedRegex(@"\|\||!!")]
    private static partial Regex CellSplitRegex();
}
