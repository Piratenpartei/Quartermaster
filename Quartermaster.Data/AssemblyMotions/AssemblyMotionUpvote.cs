using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AssemblyMotions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyMotionUpvote {
    public const string TableName = "AssemblyMotionUpvotes";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MotionId { get; set; }
    public Guid MemberId { get; set; }
    public DateTime CreatedAt { get; set; }
}
