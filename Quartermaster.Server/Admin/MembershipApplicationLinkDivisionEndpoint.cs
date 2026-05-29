using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.MembershipApplications;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationLinkDivisionRequest {
    public Guid Id { get; set; }

    /// <summary>The chosen administrative division. Ignored when <see cref="NotInGermany"/> is true.</summary>
    public Guid? AdministrativeDivisionId { get; set; }

    /// <summary>Confirms the address is outside Germany: leaves the division empty and routes to the root chapter.</summary>
    public bool NotInGermany { get; set; }
}

/// <summary>
/// Assigns a chapter (and, for German addresses, an administrative division) to an application
/// stuck in <see cref="ApplicationStatus.PendingDivisionLinking"/>, then moves it to Pending and
/// kicks off the normal officer-review flow.
/// </summary>
public class MembershipApplicationLinkDivisionEndpoint : Endpoint<MembershipApplicationLinkDivisionRequest> {
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly ApplicationReviewService _reviewService;
    private readonly PermissionContext _perms;

    public MembershipApplicationLinkDivisionEndpoint(
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo,
        ApplicationReviewService reviewService,
        PermissionContext perms) {
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _reviewService = reviewService;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/admin/membershipapplications/link-division");
    }

    public override async Task HandleAsync(MembershipApplicationLinkDivisionRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.LinkApplicationDivision)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var application = _applicationRepo.Get(req.Id);
        if (application == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (application.Status != ApplicationStatus.PendingDivisionLinking) {
            AddError(r => r.Id, I18nKey.Error.Admin.Application.NotPendingDivisionLinking);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        Guid? divisionId;
        Guid? chapterId;
        if (req.NotInGermany) {
            // No division in our (Germany-only) data set; route to the top-level chapter for review.
            divisionId = null;
            chapterId = _chapterRepo.GetRootChapter()?.Id;
            if (chapterId == null) {
                AddError(r => r.Id, I18nKey.Error.Admin.Application.NoChapterForDivision);
                await SendErrorsAsync(cancellation: ct);
                return;
            }
        } else {
            if (!req.AdministrativeDivisionId.HasValue) {
                AddError(r => r.AdministrativeDivisionId, I18nKey.Error.Admin.Application.DivisionRequired);
                await SendErrorsAsync(cancellation: ct);
                return;
            }
            var chapter = _chapterRepo.FindForDivision(req.AdministrativeDivisionId.Value, _adminDivRepo);
            if (chapter == null) {
                AddError(r => r.AdministrativeDivisionId, I18nKey.Error.Admin.Application.NoChapterForDivision);
                await SendErrorsAsync(cancellation: ct);
                return;
            }
            divisionId = req.AdministrativeDivisionId.Value;
            chapterId = chapter.Id;
        }

        _applicationRepo.LinkDivisionAndChapter(application.Id, divisionId, chapterId);

        var linked = _applicationRepo.Get(application.Id);
        if (linked != null) {
            _reviewService.CreateReviewMotionAndNotify(linked);
        }

        await SendOkAsync(ct);
    }
}
