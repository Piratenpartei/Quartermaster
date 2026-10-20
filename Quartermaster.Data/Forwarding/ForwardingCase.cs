using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Forwarding;

namespace Quartermaster.Data.Forwarding;

/// <summary>
/// One forwarded submission. <see cref="PayloadJson"/> is the only copy of the submitted personal data on the Redmine path and is cleared once every ticket exists.
/// </summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class ForwardingCase {
    public const string TableName = "ForwardingCases";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public ForwardingCaseKind Kind { get; set; }
    public Guid? ApplicationChapterId { get; set; }
    public Guid? DecidingChapterId { get; set; }
    public Guid? StateChapterId { get; set; }
    public Guid? RelatedCaseId { get; set; }
    public Guid? MembershipApplicationId { get; set; }
    public Guid? DueSelectionId { get; set; }
    public Guid? MotionId { get; set; }
    public string? PayloadJson { get; set; }
    public ForwardingCaseStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [NotColumn]
    public string Reference => ReferenceFor(Id);

    /// <summary>Short case reference written into every ticket description, for example <c>QM-0A1B2C3D</c>.</summary>
    public static string ReferenceFor(Guid id) {
        return "QM-" + id.ToString("N")[..8].ToUpperInvariant();
    }
}
