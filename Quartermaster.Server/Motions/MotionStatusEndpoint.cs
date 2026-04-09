using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Motions;

public class MotionStatusEndpoint : Endpoint<MotionStatusRequest> {
    private readonly MotionRepository _motionRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public MotionStatusEndpoint(MotionRepository motionRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _motionRepo = motionRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/motions/status");
    }

    public override async Task HandleAsync(MotionStatusRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.MotionId);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasPermission(userId.Value, motion.ChapterId, PermissionIdentifier.EditMotions, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (req.ApprovalStatus.HasValue)
        {
            var status = (MotionApprovalStatus)req.ApprovalStatus.Value;
            if (status != MotionApprovalStatus.FormallyRejected && status != MotionApprovalStatus.ClosedWithoutAction)
            {
                await SendErrorsAsync(400, ct);
                return;
            }
            _motionRepo.UpdateApprovalStatus(req.MotionId, status);
        }

        if (req.IsRealized.HasValue)
            _motionRepo.SetRealized(req.MotionId, req.IsRealized.Value);

        await SendOkAsync(ct);
    }
}
