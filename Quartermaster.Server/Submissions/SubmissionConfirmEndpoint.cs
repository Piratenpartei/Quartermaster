using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Submissions;

namespace Quartermaster.Server.Submissions;

/// <summary>
/// Confirms a public submission via its email-link token, materializing the real entity.
/// Anonymous + single-use: the atomic claim in the repo prevents double materialization
/// from concurrent clicks.
/// </summary>
public class SubmissionConfirmEndpoint : EndpointWithoutRequest<SubmissionConfirmResultDTO> {
    private readonly PendingSubmissionRepository _pendingRepo;
    private readonly SubmissionMaterializer _materializer;

    public SubmissionConfirmEndpoint(PendingSubmissionRepository pendingRepo, SubmissionMaterializer materializer) {
        _pendingRepo = pendingRepo;
        _materializer = materializer;
    }

    public override void Configure() {
        Post("/api/submissions/{Token}/confirm");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var token = Route<string>("Token") ?? "";
        var pending = _pendingRepo.Get(token);
        if (pending == null) {
            await SendResult(SubmissionConfirmStatus.NotFound, ct);
            return;
        }
        if (pending.ConfirmedAt != null) {
            await SendResult(SubmissionConfirmStatus.AlreadyConfirmed, ct);
            return;
        }
        var now = DateTime.UtcNow;
        if (pending.ExpiresAt < now) {
            await SendResult(SubmissionConfirmStatus.Expired, ct);
            return;
        }
        if (!_pendingRepo.TryClaim(token, now)) {
            // Lost the race to a concurrent confirm — the other one materialized it.
            await SendResult(SubmissionConfirmStatus.AlreadyConfirmed, ct);
            return;
        }

        await _materializer.MaterializeAsync(pending, ct);
        await SendResult(SubmissionConfirmStatus.Confirmed, ct);
    }

    private Task SendResult(SubmissionConfirmStatus status, CancellationToken ct)
        => SendAsync(new SubmissionConfirmResultDTO { Status = status }, cancellation: ct);
}
