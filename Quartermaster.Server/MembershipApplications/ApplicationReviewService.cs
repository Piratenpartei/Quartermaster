using System;
using System.Collections.Generic;
using System.Linq;
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
/// Creates the officer-review motions for a membership application and notifies the deciding chapter's
/// processing officers. Used by the forwarding intake at confirm time and after division linking. No-op without a chapter.
/// </summary>
public class ApplicationReviewService {
    private const string FederalApprovalNotice = "\nBenötigt zusätzlich die Zustimmung des Bundesvorstands (§ 3 Abs. 2 BS).\n";

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
    /// <param name="onBehalfOf">The marked chapter the application's chapter decides for, or null.</param>
    public Guid? CreateReviewMotionAndNotify(MembershipApplication application, bool isReduced, ChapterMarkSkip? onBehalfOf) {
        if (!application.ChapterId.HasValue)
            return null;

        var md = ApplicationMarkdown(application);

        if (isReduced)
            md += "\nÜber die beantragte Beitragsminderung wird gesondert entschieden.\n";

        if (application.RequiresFederalApproval)
            md += FederalApprovalNotice;

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
            linkedDueSelectionId: null,
            onBehalfOf: onBehalfOf);
        _motionRepo.Create(motion);
        NotifyApplicationSubmitted(application, application.ChapterId.Value, isReduced);
        return motion.Id;
    }

    /// <summary>
    /// Takes the federal approval notice out of the application's open motions once the requirement is lifted; their votes stay. The notice
    /// line is found even when an officer's edit of the motion changed its whitespace or case. Runs inside the caller's transaction.
    /// </summary>
    public void RemoveFederalApprovalNotice(Guid applicationId) {
        foreach (var motion in _motionRepo.ListByLinkedApplicationId(applicationId)) {
            if (motion.ApprovalStatus != MotionApprovalStatus.Pending)
                continue;
            var md = WithoutFederalApprovalNotice(motion.TextMarkdown);
            if (md != motion.TextMarkdown)
                _motionRepo.UpdateText(motion.Id, md, MarkdownService.ToHtml(md, SanitizationProfile.Strict));
        }
    }

    /// <summary>Also drops the blank line the notice brought along, so the paragraphs around it keep a single gap.</summary>
    private static string WithoutFederalApprovalNotice(string markdown) {
        var notice = ComparableLine(FederalApprovalNotice);
        var lines = markdown.Split('\n');
        var kept = new List<string>();
        for (var i = 0; i < lines.Length; i++) {
            if (ComparableLine(lines[i]) != notice) {
                kept.Add(lines[i]);
                continue;
            }
            if (SitsBetweenBlankLines(kept, lines, i))
                i++;
        }
        return string.Join('\n', kept);
    }

    private static bool SitsBetweenBlankLines(List<string> kept, string[] lines, int index) {
        if (kept.Count == 0)
            return false;
        if (!string.IsNullOrWhiteSpace(kept[^1]))
            return false;
        if (index + 1 >= lines.Length)
            return false;
        return string.IsNullOrWhiteSpace(lines[index + 1]);
    }

    private static string ComparableLine(string line) {
        return string.Concat(line.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
    }

    /// <summary>
    /// Creates the motion on the Bundesvorstand's approval under § 3 Abs. 2 BS after the chapter approved, linked to the same application, and
    /// notifies the federal processors. <paramref name="hearing"/> says which chapter decided, when and how. <paramref name="motionId"/> is the id
    /// the caller already claimed on the application.
    /// </summary>
    /// <param name="chapterDecidedAtUtc">The chapter's decision, stored as the motion's creation time; an approval after the federal step was
    /// lifted takes its date from there.</param>
    /// <param name="isReduced">True when the application's dues need their own decision.</param>
    public void CreateFederalApprovalMotion(MembershipApplication application, Guid motionId, Guid federalChapterId, string hearing, DateTime chapterDecidedAtUtc,
        bool isReduced) {
        var md = ApplicationMarkdown(application)
            + $"\nAnhörung: {hearing}\n"
            + $"\n[Antrag ansehen](/Administration/MembershipApplications/{application.Id})\n";

        var motion = Motion.Create(
            federalChapterId,
            authorName: $"{application.FirstName} {application.LastName}",
            authorEmail: application.Email,
            title: $"Mitgliedsantrag (Zustimmung nach § 3 Abs. 2 BS): {application.FirstName} {application.LastName}",
            textMarkdown: md,
            textHtml: MarkdownService.ToHtml(md, SanitizationProfile.Strict),
            nowUtc: chapterDecidedAtUtc,
            linkedApplicationId: application.Id,
            linkedDueSelectionId: null);
        motion.Id = motionId;
        _motionRepo.Create(motion);
        NotifyApplicationSubmitted(application, federalChapterId, isReduced);
    }

    /// <summary>Creates the board motion on a reduced due selection in the deciding chapter. The vote sets the due selection to approved or rejected.</summary>
    public Guid CreateReductionMotion(DueSelection dueSelection, Guid chapterId, MembershipApplication? application, ChapterMarkSkip? onBehalfOf) {
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
            linkedDueSelectionId: dueSelection.Id,
            onBehalfOf: onBehalfOf);
        _motionRepo.Create(motion);
        return motion.Id;
    }

    public void NotifyApplicationSubmitted(MembershipApplication application, Guid chapterId, bool isReduced) {
        var chapter = _chapterRepo.Get(chapterId);
        var chapterName = chapter?.Name ?? "";
        var payload = new ApplicationSubmittedPayload(
            application.Id, chapterId, chapterName,
            application.FirstName, application.LastName, isReduced);
        _notifications.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.ApplicationSubmitted,
            payload,
            _ => new Dictionary<string, object> {
                ["application"] = application.ToDetailDto(chapterName, isReduced),
                ["chapter"] = _chapterRepo.WithTemplateContact(chapter?.ToDto() ?? new ChapterDTO { Id = chapterId, Name = chapterName })
            },
            SourceEntityType: "MembershipApplication",
            SourceEntityId: application.Id));
    }

    public void NotifyDueSelectionSubmitted(DueSelection record) {
        if (record.ChapterId == null)
            return;

        var chapterId = record.ChapterId.Value;
        var chapter = _chapterRepo.Get(chapterId);
        var chapterName = chapter?.Name ?? "";
        var payload = new DueSelectionSubmittedPayload(record.Id, chapterId, chapterName, record.FirstName, record.LastName, record.SelectedDue);
        _notifications.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.DueSelectionSubmitted,
            payload,
            _ => new Dictionary<string, object> {
                ["selection"] = record.ToDetailDto(),
                ["chapter"] = _chapterRepo.WithTemplateContact(chapter?.ToDto() ?? new ChapterDTO { Id = chapterId, Name = chapterName })
            },
            SourceEntityType: "DueSelection",
            SourceEntityId: record.Id));
    }

    private static string ApplicationMarkdown(MembershipApplication application) {
        return $"**Mitgliedsantrag von {application.FirstName} {application.LastName}**\n\n"
            + $"- **E-Mail:** {application.Email}\n"
            + $"- **Adresse:** {application.AddressStreet} {application.AddressHouseNbr}, "
            + $"{application.AddressPostCode} {application.AddressCity}\n";
    }
}
