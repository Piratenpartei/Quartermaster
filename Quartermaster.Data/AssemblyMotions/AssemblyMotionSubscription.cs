using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AssemblyMotions;

/// <summary>A user's subscription to a motion's forum replies, made by hand or by supporting the motion.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyMotionSubscription {
    public const string TableName = "AssemblyMotionSubscriptions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MotionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
