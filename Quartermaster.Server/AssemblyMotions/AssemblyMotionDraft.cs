using System;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions;

public record AssemblyMotionDraft(Guid TypeId, string Title, string? Section, string TextMarkdown, string ReasonMarkdown,
    string? Summary, string? Keywords, string? PriorDiscussion) {
    public AssemblyMotionDraft Normalized() {
        return this with {
            Title = NormalizeSingleLine(Title),
            Section = NormalizeOptional(Section),
            TextMarkdown = NormalizeMarkdown(TextMarkdown),
            ReasonMarkdown = NormalizeMarkdown(ReasonMarkdown),
            Summary = NormalizeOptional(Summary),
            Keywords = NormalizeOptional(Keywords),
            PriorDiscussion = NormalizeOptional(PriorDiscussion)
        };
    }

    private static string? NormalizeOptional(string? value) {
        var normalized = NormalizeSingleLine(value);
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string NormalizeSingleLine(string? value) {
        return TextNormalization.RemoveControlCharacters(TextNormalization.ToNfc(value)).Trim();
    }

    private static string NormalizeMarkdown(string value) {
        return MotionMarkdown.Normalize(value);
    }
}
