using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AssemblyMotions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyMotionSubmitter {
    public const string TableName = "AssemblyMotionSubmitters";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MotionId { get; set; }
    public Guid MemberId { get; set; }
    public bool IsAuthor { get; set; }
    public DateTime JoinedAt { get; set; }
    public int JoinedRevision { get; set; }
}
