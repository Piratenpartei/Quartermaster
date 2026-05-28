using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Members;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.DueSelector;

public class DueSelectionCreateEndpoint : Endpoint<DueSelectionDTO> {
    private readonly DueSelectionRepository _dueSelectionRepository;
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly INotificationDispatchQueue _notifications;

    public DueSelectionCreateEndpoint(
        DueSelectionRepository dueSelectionRepository,
        MemberRepository memberRepo,
        ChapterRepository chapterRepo,
        INotificationDispatchQueue notifications) {
        _dueSelectionRepository = dueSelectionRepository;
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
        _notifications = notifications;
    }

    public override void Configure() {
        Post("/api/dueselector");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(DueSelectionDTO req, CancellationToken ct) {
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
        _dueSelectionRepository.Create(dueSelection);

        // Standalone due-selections can only be routed to a chapter via the member's
        // MemberNumber. When unknown (e.g. submitted before joining), the notification
        // is skipped — the membership-application flow has its own dispatch.
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

        await SendOkAsync(ct);
    }
}
