using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public class AssemblyMotionFieldValues {
    public string TypeId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Section { get; set; } = "";
    public string TextMarkdown { get; set; } = "";
    public string ReasonMarkdown { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Keywords { get; set; } = "";
    public string PriorDiscussion { get; set; } = "";

    /// <summary>
    /// Switches to the type with <paramref name="typeId"/>. A text that is still empty or still the previous type's default text
    /// is replaced by the new type's default text; anything the author wrote stays.
    /// </summary>
    public void ChangeType(string typeId, IReadOnlyList<AssemblyMotionTypeDTO> types) {
        var previous = types.FirstOrDefault(t => t.Id.ToString() == TypeId);
        var next = types.FirstOrDefault(t => t.Id.ToString() == typeId);
        var isUntouched = IsUntouchedText(previous);
        TypeId = typeId;

        if (next != null && isUntouched)
            TextMarkdown = next.DefaultText;
    }

    private bool IsUntouchedText(AssemblyMotionTypeDTO? previous) {
        if (string.IsNullOrWhiteSpace(TextMarkdown))
            return true;
        if (previous == null)
            return false;

        return Comparable(TextMarkdown) == Comparable(previous.DefaultText);
    }

    private static string Comparable(string text) {
        return text.ReplaceLineEndings("\n").Trim();
    }

    public void Fill(AssemblyMotionDetailDTO detail) {
        TypeId = detail.TypeId.ToString();
        Title = detail.Title;
        Section = detail.Section ?? "";
        TextMarkdown = detail.TextMarkdown;
        ReasonMarkdown = detail.ReasonMarkdown;
        Summary = detail.Summary ?? "";
        Keywords = detail.Keywords ?? "";
        PriorDiscussion = detail.PriorDiscussion ?? "";
    }

    /// <summary>Mirrors the server: only a change of type, section, text or reason removes the co-submitters of a released motion.</summary>
    public bool HasBindingChange(AssemblyMotionDetailDTO detail) {
        return TypeId != detail.TypeId.ToString()
            || NullIfBlank(Section) != detail.Section
            || TextMarkdown != detail.TextMarkdown
            || ReasonMarkdown != detail.ReasonMarkdown;
    }

    public bool TryApplyTo(AssemblyMotionSaveRequest request) {
        if (!Guid.TryParse(TypeId, out var typeId))
            return false;

        request.TypeId = typeId;
        request.Title = Title;
        request.Section = NullIfBlank(Section);
        request.TextMarkdown = TextMarkdown;
        request.ReasonMarkdown = ReasonMarkdown;
        request.Summary = NullIfBlank(Summary);
        request.Keywords = NullIfBlank(Keywords);
        request.PriorDiscussion = NullIfBlank(PriorDiscussion);
        return true;
    }

    private static string? NullIfBlank(string value) {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
