using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Motions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Motion {
    public const string TableName = "Motions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChapterId { get; set; }

    public string AuthorName { get; set; } = "";
    public string AuthorEMail { get; set; } = "";

    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsPublic { get; set; } = true;

    public Guid? LinkedMembershipApplicationId { get; set; }
    public Guid? LinkedDueSelectionId { get; set; }

    public MotionApprovalStatus ApprovalStatus { get; set; }
    public bool IsRealized { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public enum MotionApprovalStatus {
    Pending,
    Approved,
    Rejected,
    FormallyRejected,
    ClosedWithoutAction
}
