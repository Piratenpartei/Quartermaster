using System;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionRevisionDTO {
    public int Revision { get; set; }
    public string Title { get; set; } = "";
    public string? Section { get; set; }
    public string TextMarkdown { get; set; } = "";
    public string ReasonMarkdown { get; set; } = "";
    public string? Summary { get; set; }
    public string? Keywords { get; set; }
    public string? PriorDiscussion { get; set; }
    public DateTimeOffset EditedAt { get; set; }
    public bool EditedByCommission { get; set; }
    public string? ChangeNote { get; set; }
}
