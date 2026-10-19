using System;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

public static class AssemblyMotionWikiPage {
    public const int MaxPrefixLength = 300;
    private const string ForbiddenPrefixCharacters = "#<>[]|{}";

    public static string Title(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion) {
        return $"{portal.WikiPagePrefix?.Trim().Trim('/')}/{AssemblyMotionNumber.Format(type.Code, motion.Number!.Value)}";
    }

    /// <summary>The page URL next to the MediaWiki API, for example https://wiki.example.org/index.php?title=Antrag:PA001.</summary>
    public static string Url(string apiUrl, string title) {
        var trimmed = apiUrl.Trim();
        var root = trimmed.EndsWith("api.php", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^"api.php".Length]
            : trimmed.TrimEnd('/') + "/";
        var escaped = Uri.EscapeDataString(title.Replace(' ', '_'))
            .Replace("%2F", "/")
            .Replace("%3A", ":");
        return $"{root}index.php?title={escaped}";
    }

    /// <summary>A prefix is valid when it has no surrounding whitespace or slash, no wikitext/title-special characters, no double slash and fits the column.</summary>
    public static bool IsValidPrefix(string? prefix) {
        if (string.IsNullOrEmpty(prefix))
            return true;
        if (prefix.Length > MaxPrefixLength)
            return false;
        if (prefix != prefix.Trim() || prefix.StartsWith('/') || prefix.EndsWith('/'))
            return false;
        if (prefix.Contains("//"))
            return false;

        return !prefix.Any(c => ForbiddenPrefixCharacters.Contains(c));
    }
}
