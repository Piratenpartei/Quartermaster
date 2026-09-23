using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Quartermaster.Server.NewsPosts.Programme;

public static partial class WikitextSectionSplitter {
    public static List<WikiSection> Split(string pageTitle, string wikitext) {
        var sections = new List<WikiSection>();
        var headingStack = new List<(int Level, string Heading)>();
        string? currentHeading = null;
        var body = new StringBuilder();

        void Flush() {
            if (currentHeading == null) {
                body.Clear();
                return;
            }
            var text = WikiMarkupStripper.Strip(body.ToString());
            if (text.Length > 0) {
                var path = string.Join(" > ", new[] { pageTitle }.Concat(headingStack.Select(h => h.Heading)));
                sections.Add(new WikiSection(currentHeading, path, ToAnchor(currentHeading), text));
            }
            body.Clear();
        }

        foreach (var line in wikitext.Split('\n')) {
            var match = HeadingRegex().Match(line.Trim());
            if (!match.Success) {
                body.AppendLine(line);
                continue;
            }

            Flush();
            var level = match.Groups[1].Value.Length;
            var heading = WikiMarkupStripper.Strip(match.Groups[2].Value);
            headingStack.RemoveAll(h => h.Level >= level);
            headingStack.Add((level, heading));
            currentHeading = heading;
        }
        Flush();

        return Disambiguate(sections);
    }

    // The sync keys stored sections by heading path, so repeated headings on one page need distinct paths.
    private static List<WikiSection> Disambiguate(List<WikiSection> sections) {
        var seen = new Dictionary<string, int>();
        var result = new List<WikiSection>();
        foreach (var section in sections) {
            var count = seen.GetValueOrDefault(section.HeadingPath) + 1;
            seen[section.HeadingPath] = count;
            result.Add(count == 1 ? section : section with { HeadingPath = $"{section.HeadingPath} ({count})" });
        }
        return result;
    }

    public static string ToAnchor(string heading) => heading.Trim().Replace(' ', '_');

    [GeneratedRegex(@"^(={2,6})\s*(.+?)\s*\1\s*$")]
    private static partial Regex HeadingRegex();
}
