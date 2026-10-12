using System;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions;

public record AssemblyMotionDraft(Guid TypeId, string Title, string? Section, string TextMarkdown, string ReasonMarkdown,
    string? Summary, string? Keywords, string? PriorDiscussion) {
    public AssemblyMotionDraft Normalized() => this with {
        Title = TextNormalization.ToNfc(Title).Trim(),
        Section = NormalizeOptional(Section),
        TextMarkdown = NormalizeMarkdown(TextMarkdown),
        ReasonMarkdown = NormalizeMarkdown(ReasonMarkdown),
        Summary = NormalizeOptional(Summary),
        Keywords = NormalizeOptional(Keywords),
        PriorDiscussion = NormalizeOptional(PriorDiscussion)
    };

    private static string? NormalizeOptional(string? value) {
        var trimmed = TextNormalization.ToNfc(value).Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string NormalizeMarkdown(string value)
        => TextNormalization.ToNfc(value).Replace("\r\n", "\n").TrimEnd();
}
