using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Changelog;
using Quartermaster.Rendering;

namespace Quartermaster.Server.Changelog;

/// <summary>
/// Reads <c>Changelog/V*.md</c> once at startup, renders each via <see cref="MarkdownService"/>,
/// and caches the result for /api/changelog. App restart picks up edits — files are deploy-time content, not runtime data.
/// </summary>
public class ChangelogService {
    private readonly List<ChangelogEntryDTO> _entries;

    public ChangelogService(ILogger<ChangelogService> logger) {
        _entries = Load(logger);
    }

    public IReadOnlyList<ChangelogEntryDTO> GetAll() => _entries;

    private static List<ChangelogEntryDTO> Load(ILogger logger) {
        var dir = Path.Combine(AppContext.BaseDirectory, "Changelog");
        if (!Directory.Exists(dir)) {
            logger.LogWarning("Changelog directory not found at {Dir}", dir);
            return [];
        }

        var entries = new List<(Version Sort, string Display, string Html)>();
        foreach (var path in Directory.EnumerateFiles(dir, "V*.md")) {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!TryParseVersion(fileName, out var version)) {
                logger.LogWarning("Skipping changelog file with unparseable version: {File}", fileName);
                continue;
            }
            var markdown = File.ReadAllText(path);
            var html = MarkdownService.ToHtml(markdown, SanitizationProfile.Standard);
            entries.Add((version, fileName[1..], html));
        }

        return entries
            .OrderByDescending(e => e.Sort)
            .Select(e => new ChangelogEntryDTO { Version = e.Display, Html = e.Html })
            .ToList();
    }

    private static readonly Regex VersionPattern = new(@"^V(\d+(?:\.\d+)*)$", RegexOptions.Compiled);

    private static bool TryParseVersion(string fileName, out Version version) {
        version = new Version();
        var match = VersionPattern.Match(fileName);
        if (!match.Success)
            return false;
        // Version.Parse needs at least major.minor; pad single-segment names like "V2" to "V2.0".
        var raw = match.Groups[1].Value;
        if (!raw.Contains('.'))
            raw += ".0";
        return Version.TryParse(raw, out version!);
    }
}
