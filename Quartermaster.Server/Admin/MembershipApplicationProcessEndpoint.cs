using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationProcessRequest {
    public Guid Id { get; set; }
    public int Status { get; set; }
}

public class MembershipApplicationProcessEndpoint : Endpoint<MembershipApplicationProcessRequest> {
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public MembershipApplicationProcessEndpoint(MembershipApplicationRepository applicationRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _applicationRepo = applicationRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/admin/membershipapplications/process");
    }

    public override async Task HandleAsync(MembershipApplicationProcessRequest req, CancellationToken ct) {
        var application = _applicationRepo.Get(req.Id);
        if (application == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (application.ChapterId.HasValue) {
            if (!EndpointAuthorizationHelper.HasPermission(userId.Value, application.ChapterId.Value, PermissionIdentifier.ProcessApplications, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ProcessApplications, _globalPermRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        var status = (ApplicationStatus)req.Status;
        if (status != ApplicationStatus.Approved && status != ApplicationStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _applicationRepo.UpdateStatus(req.Id, status, null);
        await SendOkAsync(ct);
    }
}
