using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Admin;

public class DueSelectionProcessRequest {
    public Guid Id { get; set; }
    public int Status { get; set; }
}

public class DueSelectionProcessEndpoint : Endpoint<DueSelectionProcessRequest> {
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;

    public DueSelectionProcessEndpoint(DueSelectionRepository dueSelectionRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        MembershipApplicationRepository applicationRepo, ChapterRepository chapterRepo) {
        _dueSelectionRepo = dueSelectionRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/admin/dueselections/process");
    }

    public override async Task HandleAsync(DueSelectionProcessRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var selection = _dueSelectionRepo.Get(req.Id);
        if (selection == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var application = _applicationRepo.GetByDueSelectionId(selection.Id);
        if (application?.ChapterId.HasValue == true) {
            if (!EndpointAuthorizationHelper.HasPermission(userId.Value, application.ChapterId.Value, PermissionIdentifier.ProcessDueSelections, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ProcessDueSelections, _globalPermRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        var status = (DueSelectionStatus)req.Status;
        if (status != DueSelectionStatus.Approved && status != DueSelectionStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _dueSelectionRepo.UpdateStatus(req.Id, status, null);
        await SendOkAsync(ct);
    }
}
