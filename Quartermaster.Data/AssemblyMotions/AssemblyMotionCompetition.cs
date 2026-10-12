using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AssemblyMotions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyMotionCompetition {
    public const string TableName = "AssemblyMotionCompetitions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The smaller of the two motion ids by <see cref="Guid.CompareTo"/>; callers must normalize the pair before inserting.</summary>
    public Guid MotionAId { get; set; }
    public Guid MotionBId { get; set; }

    /// <summary>The motion whose submitter or commission view declared the link; the spec's removal rights depend on it.</summary>
    public Guid DeclaringMotionId { get; set; }

    public Guid DeclaredByUserId { get; set; }
    public DateTime DeclaredAt { get; set; }
    public bool DeclaredByCommission { get; set; }
}
