using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Submissions;

/// <summary>
/// Kind of public submission awaiting email confirmation. Persisted as int — append only,
/// never reorder existing members.
/// </summary>
public enum PendingSubmissionKind {
    Motion = 0,
    DueSelection = 1,
    MembershipApplication = 2
}

/// <summary>
/// A public, unauthenticated submission held until the submitter confirms their email.
/// The real entity (motion / due selection / membership application) is only created on
/// confirmation; until then nothing but this row exists, so spam never reaches the live
/// tables. Unconfirmed rows are swept after <see cref="PendingSubmissionRepository.Lifetime"/>.
/// </summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class PendingSubmission {
    public const string TableName = "PendingSubmissions";

    [PrimaryKey]
    public string Token { get; set; } = "";
    public PendingSubmissionKind Kind { get; set; }
    public string PayloadJson { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
