using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AssemblyMotions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyMotionRevision {
    public const string TableName = "AssemblyMotionRevisions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MotionId { get; set; }
    public int Revision { get; set; }
    public string Title { get; set; } = "";
    public string? Section { get; set; }
    public string TextMarkdown { get; set; } = "";
    public string ReasonMarkdown { get; set; } = "";
    public string? Summary { get; set; }
    public string? Keywords { get; set; }
    public string? PriorDiscussion { get; set; }
    public Guid EditedByUserId { get; set; }
    public DateTime EditedAt { get; set; }
    public bool EditedByCommission { get; set; }
    public string? ChangeNote { get; set; }
}
