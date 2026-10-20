using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <summary>Builds the topic title and the first post of a mirrored motion.</summary>
public static partial class AssemblyMotionForumPost {
    public const int MaxTitleLength = 255;

    private const string Ellipsis = "...";
    private const string Footer = "_Dieser Beitrag wird automatisch aus dem Antragsportal aktualisiert, Änderungen hier werden überschrieben._";

    /// <summary>The number (the reference number until the motion has its official one), title and event.</summary>
    public static string Title(AssemblyMotionForumInput input) {
        var label = AssemblyMotionNumber.Label(input.Type.Code, input.Motion.Number, input.Motion.ReferenceNumber);
        var number = label == null ? "" : label + ": ";
        var title = input.Motion.Title;
        var suffix = $" ({input.EventName})";
        var full = $"{number}{title}{suffix}";
        if (full.Length <= MaxTitleLength)
            return full;

        var available = MaxTitleLength - number.Length - Ellipsis.Length - suffix.Length;
        if (available < 1)
            return full[..(MaxTitleLength - Ellipsis.Length)] + Ellipsis;

        return $"{number}{title[..available]}{Ellipsis}{suffix}";
    }

    /// <summary>
    /// The first post. The motion text, then the reason, is replaced by a notice when the post would exceed <paramref name="maxPostLength"/>;
    /// <paramref name="forceNotices"/> replaces both, for a forum that refused the longer version.
    /// </summary>
    public static string Raw(AssemblyMotionForumInput input, int maxPostLength, bool forceNotices = false) {
        if (forceNotices)
            return Build(input, false, false);

        var withText = Build(input, true, false).Length <= maxPostLength;
        var withReason = Build(input, withText, true).Length <= maxPostLength;
        return Build(input, withText, withReason);
    }

    /// <summary>Lowercase hex SHA-256 over title and post text.</summary>
    public static string Hash(string title, string raw) {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(title + "\n" + raw)));
    }

    /// <summary>
    /// A backslash before an @ that starts a word keeps Discourse from notifying the named user; the post still shows "@name".
    /// An @ behind an even run of backslashes is still live (each pair is a literal backslash), so it gets one more.
    /// </summary>
    public static string DefuseMentions(string text) {
        return MentionPattern().Replace(text, match => {
            var backslashes = match.Groups[1].Length;
            if (backslashes % 2 == 1)
                return match.Value;
            if (backslashes == 0 && match.Index > 0 && IsWordCharacter(text[match.Index - 1]))
                return match.Value;
            return match.Groups[1].Value + "\\@";
        });
    }

    private static bool IsWordCharacter(char character) {
        return char.IsLetterOrDigit(character) || character == '_';
    }

    private static string Build(AssemblyMotionForumInput input, bool withText, bool withReason) {
        var motion = input.Motion;
        var label = AssemblyMotionNumber.Label(input.Type.Code, motion.Number, motion.ReferenceNumber);
        var number = label == null ? "" : " " + label;
        var builder = new StringBuilder();

        builder.Append($"{input.Type.Name}{number}: {DefuseMentions(motion.Title)}\n");
        if (input.SubmitterNames.Count > 0)
            builder.Append($"Antragsteller: {string.Join(", ", input.SubmitterNames.Select(DefuseMentions))}\n");
        builder.Append($"Status: {input.StatusLabel}\n");
        if (!string.IsNullOrEmpty(input.ResultLabel))
            builder.Append($"Ergebnis: {input.ResultLabel}\n");
        if (!string.IsNullOrWhiteSpace(motion.Section))
            builder.Append($"Antragsgruppe: {DefuseMentions(motion.Section)}\n");

        if (motion.Status == AssemblyMotionStatus.CollectingCoSubmitters)
            builder.Append($"\n**{CollectingNote(input)}**\n");

        builder.Append("\n### Zusammenfassung\n");
        builder.Append(string.IsNullOrWhiteSpace(motion.Summary)
            ? "Für diesen Antrag wurde keine Zusammenfassung angegeben."
            : DefuseMentions(motion.Summary));
        builder.Append('\n');

        AppendDetails(builder, "Antragstext", withText ? motion.TextMarkdown : Notice(input, "Der Antragstext ist"));
        if (!withReason || !string.IsNullOrWhiteSpace(motion.ReasonMarkdown))
            AppendDetails(builder, "Begründung", withReason ? motion.ReasonMarkdown : Notice(input, "Die Begründung ist"));

        builder.Append($"\nAntrag im Antragsportal: {input.PortalUrl}\n");
        if (input.WikiUrl != null)
            builder.Append($"Antrag im Wiki: {input.WikiUrl}\n");
        builder.Append($"\n{Footer}\n");

        return WikiPageText.Normalize(builder.ToString());
    }

    private static string CollectingNote(AssemblyMotionForumInput input) {
        var opening = input.Motion.IsInRevision
            ? "Dieser Antrag wird überarbeitet und sucht erneut Mitantragsteller"
            : "Dieser Antrag ist noch nicht eingereicht und sucht Mitantragsteller";
        var needed = input.Type.MinSubmitters - 1;
        return $"{opening}: Er braucht mindestens {needed} Mitantragsteller bis zum {BerlinTime.FormatDeadline(input.CollectingDeadline)}, "
            + $"bisher hat er {input.CoSubmitterCount}. Mitantragsteller werden kannst du im Antragsportal.";
    }

    private static void AppendDetails(StringBuilder builder, string label, string body) {
        builder.Append($"\n[details=\"{label}\"]\n");
        builder.Append(NeutraliseDetailsTags(DefuseMentions(WikiPageText.Normalize(body))));
        builder.Append("\n[/details]\n");
    }

    private static string Notice(AssemblyMotionForumInput input, string subject) {
        var where = input.WikiUrl == null ? "im Antragsportal" : "im Antragsportal und im Wiki";
        return $"{subject} zu lang für das Forum und steht vollständig {where}.";
    }

    // Character references do not depend on Markdown escape rules, so neither a BBCode nor an HTML details tag in user text can open or close a block.
    private static string NeutraliseDetailsTags(string text) {
        var withoutBbCode = DetailsBbCodePattern().Replace(text, "&#91;$1");
        return DetailsHtmlPattern().Replace(withoutBbCode, "&lt;$1");
    }

    [GeneratedRegex(@"(\\*)@(?=[\w.-])")]
    private static partial Regex MentionPattern();

    [GeneratedRegex(@"\[(/?details)", RegexOptions.IgnoreCase)]
    private static partial Regex DetailsBbCodePattern();

    [GeneratedRegex(@"<(/?(?:details|summary))", RegexOptions.IgnoreCase)]
    private static partial Regex DetailsHtmlPattern();
}
