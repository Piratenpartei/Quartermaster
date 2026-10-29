using System.Collections.Generic;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Motions;

/// <summary>Tells the officers of the chapter that decides on a newly submitted board motion.</summary>
public class MotionSubmittedNotifier {
    private readonly ChapterRepository _chapterRepo;
    private readonly INotificationDispatchQueue _notifications;

    public MotionSubmittedNotifier(ChapterRepository chapterRepo, INotificationDispatchQueue notifications) {
        _chapterRepo = chapterRepo;
        _notifications = notifications;
    }

    /// <param name="chapter">The chapter the motion is stored at.</param>
    public void Notify(Motion motion, Chapter chapter) {
        var chapterName = chapter.Name;
        var payload = new MotionSubmittedPayload(
            motion.Id, motion.ChapterId, motion.Title, motion.AuthorName, chapterName);
        _notifications.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.MotionSubmitted,
            payload,
            _ => new Dictionary<string, object> {
                ["motion"] = motion.ToDetailDto(chapterName),
                ["chapter"] = _chapterRepo.WithTemplateContact(chapter.ToDto())
            },
            SourceEntityType: "Motion",
            SourceEntityId: motion.Id));
    }
}
