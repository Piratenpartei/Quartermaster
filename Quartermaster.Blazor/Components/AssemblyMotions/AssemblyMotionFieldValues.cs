using System;
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

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
