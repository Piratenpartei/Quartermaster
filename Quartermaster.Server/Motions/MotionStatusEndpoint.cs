using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Motions;

public class MotionStatusEndpoint : Endpoint<MotionStatusRequest> {
    private readonly MotionRepository _motionRepo;
    private readonly PermissionContext _perms;

    public MotionStatusEndpoint(MotionRepository motionRepo, PermissionContext perms) {
        _motionRepo = motionRepo;
        _perms = perms;
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

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(motion.ChapterId, PermissionIdentifier.EditMotions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (req.ApprovalStatus.HasValue) {
            var status = req.ApprovalStatus.Value;
            if (status != MotionApprovalStatus.FormallyRejected && status != MotionApprovalStatus.ClosedWithoutAction) {
                await SendErrorsAsync(400, ct);
                return;
            }
            _motionRepo.UpdateApprovalStatus(req.MotionId, status);
        }

        if (req.IsRealized.HasValue)
            _motionRepo.SetRealized(req.MotionId, req.IsRealized.Value);

        if (req.IsPublic.HasValue)
            _motionRepo.SetPublic(req.MotionId, req.IsPublic.Value);

        await SendOkAsync(ct);
    }
}
