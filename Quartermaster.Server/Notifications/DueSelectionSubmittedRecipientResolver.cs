using System;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Notifications;

/// <summary>Notifies users holding <see cref="PermissionIdentifier.ProcessDueSelections"/> on the resolved chapter.</summary>
public class DueSelectionSubmittedRecipientResolver : ChapterPermissionRecipientResolver<DueSelectionSubmittedPayload> {
    public DueSelectionSubmittedRecipientResolver(DbContext db, ChapterRepository chapterRepo)
        : base(db, chapterRepo) { }

    public override string TriggerId => NotificationTriggers.DueSelectionSubmitted;
    protected override string PermissionIdentifier => Api.PermissionIdentifier.ProcessDueSelections;
    protected override Guid ChapterIdFor(DueSelectionSubmittedPayload payload) => payload.ChapterId;
}
