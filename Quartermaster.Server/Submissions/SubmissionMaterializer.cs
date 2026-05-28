using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Submissions;
using Quartermaster.Rendering;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Submissions;

/// <summary>
/// Creates the real entity (and fires officer notifications) for a confirmed public
/// submission. The create endpoints only stash the request; this runs at confirm time,
/// so notifications never fire for unconfirmed spam.
/// </summary>
public class SubmissionMaterializer {
    private readonly MotionRepository _motionRepo;
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly MemberRepository _memberRepo;
    private readonly INotificationDispatchQueue _notifications;
    private readonly ILogger<SubmissionMaterializer> _logger;

    public SubmissionMaterializer(
        MotionRepository motionRepo,
        DueSelectionRepository dueSelectionRepo,
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo,
        MemberRepository memberRepo,
        INotificationDispatchQueue notifications,
        ILogger<SubmissionMaterializer> logger) {
        _motionRepo = motionRepo;
        _dueSelectionRepo = dueSelectionRepo;
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
        _memberRepo = memberRepo;
        _notifications = notifications;
        _logger = logger;
    }

    public void Materialize(PendingSubmission pending) {
        switch (pending.Kind) {
            case PendingSubmissionKind.Motion:
                MaterializeMotion(Deserialize<MotionCreateRequest>(pending.PayloadJson));
                break;
            case PendingSubmissionKind.DueSelection:
                MaterializeDueSelection(Deserialize<DueSelectionDTO>(pending.PayloadJson));
                break;
            case PendingSubmissionKind.MembershipApplication:
                MaterializeApplication(Deserialize<MembershipApplicationDTO>(pending.PayloadJson));
                break;
            default:
                _logger.LogWarning("Unknown pending submission kind {Kind}", pending.Kind);
                break;
        }
    }

    private void MaterializeMotion(MotionCreateRequest req) {
        var chapter = _chapterRepo.Get(req.ChapterId);
        if (chapter == null) {
            _logger.LogWarning("Confirmed motion references a chapter that no longer exists: {ChapterId}", req.ChapterId);
            return;
        }

        var motion = new Motion {
            ChapterId = req.ChapterId,
            AuthorName = req.AuthorName,
            AuthorEmail = req.AuthorEmail,
            Title = req.Title,
            Text = MarkdownService.ToHtml(req.Text, SanitizationProfile.Strict),
            IsPublic = false,
            ApprovalStatus = MotionApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _motionRepo.Create(motion);

        var chapterName = chapter.Name;
        var payload = new MotionSubmittedPayload(
            motion.Id, motion.ChapterId, motion.Title, motion.AuthorName, chapterName);
        _notifications.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.MotionSubmitted,
            payload,
            _ => new Dictionary<string, object> {
                ["motion"] = new {
                    motion.Id,
                    motion.Title,
                    motion.AuthorName,
                    motion.CreatedAt
                },
                ["chapter"] = new { Id = motion.ChapterId, Name = chapterName }
            },
            SourceEntityType: "Motion",
            SourceEntityId: motion.Id));
    }

    private void MaterializeDueSelection(DueSelectionDTO req) {
        var dueSelection = new DueSelection {
            FirstName = req.FirstName,
            LastName = req.LastName,
            Email = req.Email,
            MemberNumber = req.MemberNumber,
            SelectedValuation = req.SelectedValuation,
            YearlyIncome = req.YearlyIncome,
            MonthlyIncomeGroup = req.MonthlyIncomeGroup,
            ReducedAmount = req.ReducedAmount,
            SelectedDue = req.SelectedDue,
            ReducedJustification = req.ReducedJustification,
            ReducedTimeSpan = req.ReducedTimeSpan,
            IsDirectDeposit = req.IsDirectDeposit,
            AccountHolder = req.AccountHolder,
            IBAN = req.IBAN,
            PaymentSchedule = req.PaymentSchedule
        };
        _dueSelectionRepo.Create(dueSelection);

        if (req.MemberNumber > 0) {
            var member = _memberRepo.GetByMemberNumber(req.MemberNumber);
            if (member?.ChapterId is { } chapterId) {
                var chapterName = _chapterRepo.Get(chapterId)?.Name ?? "";
                var payload = new DueSelectionSubmittedPayload(
                    dueSelection.Id, chapterId, chapterName,
                    dueSelection.FirstName, dueSelection.LastName, dueSelection.SelectedDue);
                _notifications.Enqueue(new NotificationDispatchRequest(
                    NotificationTriggers.DueSelectionSubmitted,
                    payload,
                    _ => new Dictionary<string, object> {
                        ["selection"] = new {
                            dueSelection.Id,
                            dueSelection.FirstName,
                            dueSelection.LastName,
                            dueSelection.Email,
                            dueSelection.SelectedDue,
                            dueSelection.ReducedAmount,
                            dueSelection.ReducedJustification
                        },
                        ["chapter"] = new { Id = chapterId, Name = chapterName }
                    },
                    SourceEntityType: "DueSelection",
                    SourceEntityId: dueSelection.Id));
            }
        }
    }

    private void MaterializeApplication(MembershipApplicationDTO req) {
        Guid? dueSelectionId = null;
        var isReduced = false;
        if (req.DueSelection != null) {
            var dueSelection = new DueSelection {
                FirstName = req.DueSelection.FirstName,
                LastName = req.DueSelection.LastName,
                Email = req.DueSelection.Email,
                MemberNumber = req.DueSelection.MemberNumber,
                SelectedValuation = req.DueSelection.SelectedValuation,
                YearlyIncome = req.DueSelection.YearlyIncome,
                MonthlyIncomeGroup = req.DueSelection.MonthlyIncomeGroup,
                ReducedAmount = req.DueSelection.ReducedAmount,
                SelectedDue = req.DueSelection.SelectedDue,
                ReducedJustification = req.DueSelection.ReducedJustification,
                ReducedTimeSpan = req.DueSelection.ReducedTimeSpan,
                IsDirectDeposit = req.DueSelection.IsDirectDeposit,
                AccountHolder = req.DueSelection.AccountHolder,
                IBAN = req.DueSelection.IBAN,
                PaymentSchedule = req.DueSelection.PaymentSchedule
            };
            isReduced = dueSelection.SelectedValuation == SelectedValuation.Reduced;
            dueSelection.Status = isReduced
                ? DueSelectionStatus.Pending
                : DueSelectionStatus.AutoApproved;
            _dueSelectionRepo.Create(dueSelection);
            dueSelectionId = dueSelection.Id;
        }

        var application = new MembershipApplication {
            FirstName = req.FirstName,
            LastName = req.LastName,
            DateOfBirth = req.DateOfBirth,
            Citizenship = req.Citizenship,
            Email = req.Email,
            PhoneNumber = req.PhoneNumber,
            AddressStreet = req.AddressStreet,
            AddressHouseNbr = req.AddressHouseNbr,
            AddressPostCode = req.AddressPostCode,
            AddressCity = req.AddressCity,
            AddressAdministrativeDivisionId = req.AddressAdministrativeDivisionId,
            ChapterId = req.ChapterId,
            ConformityDeclarationAccepted = req.ConformityDeclarationAccepted,
            HasPriorDeclinedApplication = req.HasPriorDeclinedApplication,
            IsMemberOfAnotherParty = req.IsMemberOfAnotherParty,
            ApplicationText = req.ApplicationText,
            EntryDate = req.EntryDate,
            DueSelectionId = dueSelectionId,
            SubmittedAt = DateTime.UtcNow,
            Status = ApplicationStatus.Pending
        };
        _applicationRepo.Create(application);

        if (!application.ChapterId.HasValue) {
            return;
        }

        var md = $"**Mitgliedsantrag von {application.FirstName} {application.LastName}**\n\n"
            + $"- **E-Mail:** {application.Email}\n"
            + $"- **Adresse:** {application.AddressStreet} {application.AddressHouseNbr}, "
            + $"{application.AddressPostCode} {application.AddressCity}\n";

        if (isReduced && req.DueSelection != null) {
            md += $"\n---\n\n"
                + $"**Antrag auf Beitragsminderung**\n\n"
                + $"- **Gewünschter Betrag:** {req.DueSelection.ReducedAmount}€\n"
                + $"- **Begründung:** {req.DueSelection.ReducedJustification}\n"
                + $"\n[Einstufung ansehen](/Administration/DueSelections/{dueSelectionId})\n";
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
            IsPublic = false,
            LinkedMembershipApplicationId = application.Id,
            LinkedDueSelectionId = isReduced ? dueSelectionId : null,
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

    private static T Deserialize<T>(string json) {
        return JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Pending submission payload deserialized to null for {typeof(T).Name}");
    }
}
