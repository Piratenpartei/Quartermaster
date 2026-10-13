using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionExportDTO {
    public AssemblyMotionExportPortalDTO Portal { get; set; } = new();
    public DateTimeOffset ExportedAt { get; set; }
    public List<AssemblyMotionExportItemDTO> Motions { get; set; } = [];
}

public class AssemblyMotionExportPortalDTO {
    public string EventName { get; set; } = "";
    public string? AssemblyLabel { get; set; }
}

public class AssemblyMotionExportItemDTO {
    public string DisplayNumber { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public string TypeName { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RequiredMajority RequiredMajority { get; set; }
    public string Title { get; set; } = "";
    public string? Section { get; set; }
    public string TextMarkdown { get; set; } = "";
    public string TextHtml { get; set; } = "";
    public string ReasonMarkdown { get; set; } = "";
    public string ReasonHtml { get; set; } = "";
    public string? Summary { get; set; }
    public string? Keywords { get; set; }
    public string? PriorDiscussion { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AssemblyMotionStatus Status { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AssemblyMotionResult Result { get; set; }
    public string? CheckNote { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public List<string> CompetingDisplayNumbers { get; set; } = [];
    public List<string> Submitters { get; set; } = [];
    public int? Rank { get; set; }
    public int? UpvoteCount { get; set; }
}
