using System;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Notifications;

/// <summary>Notifies users holding <see cref="PermissionIdentifier.ProcessApplications"/> on the application's chapter.</summary>
public class ApplicationSubmittedRecipientResolver : ChapterPermissionRecipientResolver<ApplicationSubmittedPayload> {
    public ApplicationSubmittedRecipientResolver(DbContext db, ChapterRepository chapterRepo)
        : base(db, chapterRepo) { }

    public override string TriggerId => NotificationTriggers.ApplicationSubmitted;
    protected override string PermissionIdentifier => Api.PermissionIdentifier.ProcessApplications;
    protected override Guid ChapterIdFor(ApplicationSubmittedPayload payload) => payload.ChapterId;
}
