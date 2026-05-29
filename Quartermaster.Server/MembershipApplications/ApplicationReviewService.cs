using System;
using System.Collections.Generic;
using Quartermaster.Api.DueSelector;
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
/// processing officers. Shared by the confirm-time materializer (chapter already assigned) and
/// the division-linking endpoint (chapter assigned later). No-op without a chapter.
/// </summary>
public class ApplicationReviewService {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly INotificationDispatchQueue _notifications;

    public ApplicationReviewService(MotionRepository motionRepo, ChapterRepository chapterRepo,
        DueSelectionRepository dueSelectionRepo, INotificationDispatchQueue notifications) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _dueSelectionRepo = dueSelectionRepo;
        _notifications = notifications;
    }

    public void CreateReviewMotionAndNotify(MembershipApplication application) {
        if (!application.ChapterId.HasValue)
            return;

        var dueSelection = application.DueSelectionId.HasValue
            ? _dueSelectionRepo.Get(application.DueSelectionId.Value)
            : null;
        var isReduced = dueSelection != null && dueSelection.SelectedValuation == SelectedValuation.Reduced;

        var md = $"**Mitgliedsantrag von {application.FirstName} {application.LastName}**\n\n"
            + $"- **E-Mail:** {application.Email}\n"
            + $"- **Adresse:** {application.AddressStreet} {application.AddressHouseNbr}, "
            + $"{application.AddressPostCode} {application.AddressCity}\n";

        if (isReduced && dueSelection != null) {
            md += $"\n---\n\n"
                + $"**Antrag auf Beitragsminderung**\n\n"
                + $"- **Gewünschter Betrag:** {dueSelection.ReducedAmount}€\n"
                + $"- **Begründung:** {dueSelection.ReducedJustification}\n"
                + $"\n[Einstufung ansehen](/Administration/DueSelections/{application.DueSelectionId})\n";
        }

        md += $"\n[Antrag ansehen](/Administration/MembershipApplications/{application.Id})\n";

        var title = isReduced
            ? $"Mitgliedsantrag + Beitragsminderung: {application.FirstName} {application.LastName}"
            : $"Mitgliedsantrag: {application.FirstName} {application.LastName}";

        var motion = new Motion {
            ChapterId = application.ChapterId.Value,
            AuthorName = $"{application.FirstName} {application.LastName}",
            AuthorEmail = application.Email,
            Title = title,
            Text = MarkdownService.ToHtml(md, SanitizationProfile.Strict),
            TextMarkdown = md,
            IsPublic = false,
            LinkedMembershipApplicationId = application.Id,
            LinkedDueSelectionId = isReduced ? application.DueSelectionId : null,
            ApprovalStatus = MotionApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _motionRepo.Create(motion);

        var chapterName = _chapterRepo.Get(application.ChapterId.Value)?.Name ?? "";
        var payload = new ApplicationSubmittedPayload(
            application.Id, application.ChapterId.Value, chapterName,
            application.FirstName, application.LastName, isReduced);
        _notifications.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.ApplicationSubmitted,
            payload,
            _ => new Dictionary<string, object> {
                ["application"] = new {
                    application.Id,
                    application.FirstName,
                    application.LastName,
                    application.Email,
                    application.SubmittedAt,
                    HasReducedDueSelection = isReduced
                },
                ["chapter"] = new { Id = application.ChapterId.Value, Name = chapterName }
            },
            SourceEntityType: "MembershipApplication",
            SourceEntityId: application.Id));
    }
}
