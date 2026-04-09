using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Admin;

public class DueSelectionDetailRequest {
    public Guid Id { get; set; }
}

public class DueSelectionDetailEndpoint
    : Endpoint<DueSelectionDetailRequest, DueSelectionDetailDTO> {

    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MotionRepository _motionRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;

    public DueSelectionDetailEndpoint(DueSelectionRepository dueSelectionRepo, MotionRepository motionRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        MembershipApplicationRepository applicationRepo, ChapterRepository chapterRepo) {
        _dueSelectionRepo = dueSelectionRepo;
        _motionRepo = motionRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/admin/dueselections/{Id}");
    }

    public override async Task HandleAsync(DueSelectionDetailRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var ds = _dueSelectionRepo.Get(req.Id);
        if (ds == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var application = _applicationRepo.GetByDueSelectionId(ds.Id);
        if (application?.ChapterId.HasValue == true) {
            if (!EndpointAuthorizationHelper.HasPermission(userId.Value, application.ChapterId.Value, PermissionIdentifier.ViewDueSelections, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewDueSelections, _globalPermRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        await SendAsync(new DueSelectionDetailDTO {
            Id = ds.Id,
            FirstName = ds.FirstName,
            LastName = ds.LastName,
            EMail = ds.EMail,
            MemberNumber = ds.MemberNumber,
            SelectedValuation = (int)ds.SelectedValuation,
            YearlyIncome = ds.YearlyIncome,
            MonthlyIncomeGroup = ds.MonthlyIncomeGroup,
            ReducedAmount = ds.ReducedAmount,
            SelectedDue = ds.SelectedDue,
            ReducedJustification = ds.ReducedJustification,
            ReducedTimeSpan = (int)ds.ReducedTimeSpan,
            IsDirectDeposit = ds.IsDirectDeposit,
            AccountHolder = ds.AccountHolder,
            IBAN = ds.IBAN,
            PaymentSchedule = (int)ds.PaymentSchedule,
            Status = (int)ds.Status,
            ProcessedAt = ds.ProcessedAt,
            LinkedMotionId = _motionRepo.GetByLinkedDueSelectionId(ds.Id)?.Id
        }, cancellation: ct);
    }
}
