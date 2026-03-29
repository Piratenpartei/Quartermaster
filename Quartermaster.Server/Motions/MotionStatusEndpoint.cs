using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Motions;

public class MotionStatusEndpoint : Endpoint<MotionStatusRequest> {
    private readonly MotionRepository _motionRepo;

    public MotionStatusEndpoint(MotionRepository motionRepo) {
        _motionRepo = motionRepo;
    }

    public override void Configure() {
        Post("/api/motions/status");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionStatusRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.MotionId);
        if (motion == null)
        {
            await SendNotFoundAsync(ct);
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
