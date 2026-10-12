using System;
using LinqToDB.Mapping;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Data.AssemblyMotions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyMotion {
    public const string TableName = "AssemblyMotions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PortalId { get; set; }
    public Guid TypeId { get; set; }
    public int? Number { get; set; }

    [NotColumn]
    public bool WasSubmitted => Number.HasValue;

    public string Title { get; set; } = "";
    public string? Section { get; set; }
    public string TextMarkdown { get; set; } = "";
    public string TextHtml { get; set; } = "";
    public string ReasonMarkdown { get; set; } = "";
    public string ReasonHtml { get; set; } = "";
    public string? Summary { get; set; }
    public string? Keywords { get; set; }
    public string? PriorDiscussion { get; set; }
    public bool IsCompetingMotion { get; set; }
    public AssemblyMotionStatus Status { get; set; }

    /// <summary>A numbered motion back in <see cref="AssemblyMotionStatus.CollectingCoSubmitters"/> after an
    /// author edit. Everyone sees such a motion with its current text and the label "In Überarbeitung".</summary>
    [NotColumn]
    public bool IsInRevision => Status == AssemblyMotionStatus.CollectingCoSubmitters && Number.HasValue;

    public AssemblyMotionResult Result { get; set; }
    public int Revision { get; set; } = 1;
    public string? CheckNote { get; set; }
    public DateTime? CheckedAt { get; set; }
    public Guid? CheckedByUserId { get; set; }
    public Guid CreatedByMemberId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
    public DateTime? CollectingReminderSentAt { get; set; }
    public WikiSyncState WikiSyncState { get; set; }
    public int? WikiSyncedRevision { get; set; }
    public string? WikiSyncedHash { get; set; }
    public DateTime? WikiLastSyncedAt { get; set; }
    public string? WikiLastError { get; set; }
    public int WikiFailureCount { get; set; }
    public DateTime? WikiNextAttemptAt { get; set; }
    public DateTime? WikiSyncRequestedAt { get; set; }
    public int? OpenSlidesMotionId { get; set; }
    public int? OpenSlidesPushedRevision { get; set; }
    public DateTime? OpenSlidesPushedAt { get; set; }
    public string? OpenSlidesLastError { get; set; }
    public DateTime? DeletedAt { get; set; }
}
