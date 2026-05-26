using System;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Notifications;

/// <summary>Notifies users holding <see cref="PermissionIdentifier.EditMotions"/> on the motion's chapter.</summary>
public class MotionSubmittedRecipientResolver : ChapterPermissionRecipientResolver<MotionSubmittedPayload> {
    public MotionSubmittedRecipientResolver(DbContext db, ChapterRepository chapterRepo)
        : base(db, chapterRepo) { }

    public override string TriggerId => NotificationTriggers.MotionSubmitted;
    protected override string PermissionIdentifier => Api.PermissionIdentifier.EditMotions;
    protected override Guid ChapterIdFor(MotionSubmittedPayload payload) => payload.ChapterId;
}
