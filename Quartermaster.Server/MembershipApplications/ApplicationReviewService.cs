using System;
using System.Collections.Generic;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Rendering;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.MembershipApplications;

/// <summary>
/// Creates the officer-review motion for a membership application and notifies the chapter's
/// processing officers. Used by the forwarding intake at confirm time and after division linking. No-op without a chapter.
/// </summary>
public class ApplicationReviewService {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly INotificationDispatchQueue _notifications;

    public ApplicationReviewService(MotionRepository motionRepo, ChapterRepository chapterRepo, INotificationDispatchQueue notifications) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _notifications = notifications;
    }

    /// <summary>
    /// Creates the board motion on a membership application in its chapter and notifies the chapter's processors. A reduced due selection of the application is decided separately (<see cref="CreateReductionMotion"/>).
    /// Returns the motion id, or null when the application has no chapter yet.
    /// </summary>
    /// <param name="isReduced">True when the application's dues need their own decision (a reduction below the regular minimum).</param>
    public Guid? CreateReviewMotionAndNotify(MembershipApplication application, bool isReduced) {
        if (!application.ChapterId.HasValue)
            return null;

        var md = $"**Mitgliedsantrag von {application.FirstName} {application.LastName}**\n\n"
            + $"- **E-Mail:** {application.Email}\n"
            + $"- **Adresse:** {application.AddressStreet} {application.AddressHouseNbr}, "
            + $"{application.AddressPostCode} {application.AddressCity}\n";

        if (isReduced)
            md += "\nÜber die beantragte Beitragsminderung wird gesondert entschieden.\n";

        md += $"\n[Antrag ansehen](/Administration/MembershipApplications/{application.Id})\n";

        var motion = Motion.Create(
            application.ChapterId.Value,
            authorName: $"{application.FirstName} {application.LastName}",
            authorEmail: application.Email,
            title: $"Mitgliedsantrag: {application.FirstName} {application.LastName}",
            textMarkdown: md,
            textHtml: MarkdownService.ToHtml(md, SanitizationProfile.Strict),
            nowUtc: DateTime.UtcNow,
            linkedApplicationId: application.Id,
            linkedDueSelectionId: null);
        _motionRepo.Create(motion);

        var chapter = _chapterRepo.Get(application.ChapterId.Value);
        var chapterName = chapter?.Name ?? "";
        var payload = new ApplicationSubmittedPayload(
            application.Id, application.ChapterId.Value, chapterName,
            application.FirstName, application.LastName, isReduced);
        _notifications.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.ApplicationSubmitted,
            payload,
            _ => new Dictionary<string, object> {
                ["application"] = application.ToDetailDto(chapterName, isReduced),
                ["chapter"] = chapter?.ToDto() ?? new ChapterDTO { Id = application.ChapterId.Value, Name = chapterName }
            },
            SourceEntityType: "MembershipApplication",
            SourceEntityId: application.Id));
        return motion.Id;
    }

    /// <summary>Creates the board motion on a reduced due selection in the deciding chapter. The vote sets the due selection to approved or rejected.</summary>
    public Guid CreateReductionMotion(DueSelection dueSelection, Guid chapterId, MembershipApplication? application) {
        var firstName = application?.FirstName ?? dueSelection.FirstName;
        var lastName = application?.LastName ?? dueSelection.LastName;
        var email = application?.Email ?? dueSelection.Email ?? "";

        var md = $"**Antrag auf Beitragsminderung von {firstName} {lastName}**\n\n"
            + (dueSelection.MemberNumber != null && dueSelection.MemberNumber != 0 ? $"- **Mitgliedsnummer:** {dueSelection.MemberNumber}\n" : "")
            + $"- **Gewünschter Jahresbeitrag:** {dueSelection.ReducedAmount}€\n"
            + $"- **Begründung:** {dueSelection.ReducedJustification}\n";
        if (application != null)
            md += $"- **Zum Mitgliedsantrag:** [Antrag ansehen](/Administration/MembershipApplications/{application.Id})\n";
        md += $"\n[Einstufung ansehen](/Administration/DueSelections/{dueSelection.Id})\n";

        var motion = Motion.Create(
            chapterId,
            authorName: $"{firstName} {lastName}",
            authorEmail: email,
            title: $"Beitragsminderung: {firstName} {lastName}",
            textMarkdown: md,
            textHtml: MarkdownService.ToHtml(md, SanitizationProfile.Strict),
            nowUtc: DateTime.UtcNow,
            linkedApplicationId: null,
            linkedDueSelectionId: dueSelection.Id);
        _motionRepo.Create(motion);
        return motion.Id;
    }
}
