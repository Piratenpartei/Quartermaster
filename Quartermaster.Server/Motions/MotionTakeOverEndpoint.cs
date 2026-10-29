using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Chapters;

namespace Quartermaster.Server.Motions;

/// <summary>
/// Moves a waiting reduced due selection to the root chapter, in one transaction, and tells the root's officers afterwards.
/// Requires <see cref="PermissionIdentifier.ProcessDueSelections"/> on the root chapter or globally.
/// </summary>
public class MotionTakeOverEndpoint : EndpointWithoutRequest<MotionTakeOverResponse> {
    private readonly MotionRepository _motionRepo;
    private readonly TakeOverPlanner _planner;
    private readonly MotionHandover _handover;
    private readonly DbContext _context;
    private readonly PermissionContext _perms;

    public MotionTakeOverEndpoint(MotionRepository motionRepo, TakeOverPlanner planner, MotionHandover handover, DbContext context,
        PermissionContext perms) {
        _motionRepo = motionRepo;
        _planner = planner;
        _handover = handover;
        _context = context;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/motions/{Id}/take-over");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var motion = _motionRepo.Get(Route<Guid>("Id"));
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var rootId = _planner.TakeOverRootFor(motion);
        if (rootId == null) {
            await SendNotPossibleAsync(ct);
            return;
        }
        if (!_perms.Has(rootId.Value, PermissionIdentifier.ProcessDueSelections)) {
            await SendForbiddenAsync(ct);
            return;
        }

        bool handedOver;
        using (var tx = _context.BeginTransaction()) {
            handedOver = _handover.HandOverMotion(motion, rootId.Value, MotionHandoverReason.TakenOver, null);
            tx.Commit();
        }
        if (!handedOver) {
            await SendNotPossibleAsync(ct);
            return;
        }
        _handover.SendNotifications();

        var replacementId = _motionRepo.Get(motion.Id)!.HandedOverToMotionId!.Value;
        await SendAsync(new MotionTakeOverResponse { MotionId = replacementId }, cancellation: ct);
    }

    private async Task SendNotPossibleAsync(CancellationToken ct) {
        AddError(I18nKey.Error.Motion.TakeOver.NotPossible);
        await SendErrorsAsync(400, ct);
    }
}
