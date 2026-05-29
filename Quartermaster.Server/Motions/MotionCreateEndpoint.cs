using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Submissions;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Submissions;

namespace Quartermaster.Server.Motions;

/// <summary>
/// Motion submission. Authenticated callers create the motion directly (already-trusted
/// origin, no spam barrier). Anonymous callers go through the email-confirm pending flow:
/// the motion is only materialized once they click the link, so unconfirmed spam never
/// reaches the live table or pings officers.
/// </summary>
public class MotionCreateEndpoint : Endpoint<MotionCreateRequest, SubmissionAcceptedResponse> {
    private readonly ChapterRepository _chapterRepo;
    private readonly SubmissionIntakeService _intake;
    private readonly SubmissionMaterializer _materializer;
    private readonly PermissionContext _perms;

    public MotionCreateEndpoint(ChapterRepository chapterRepo, SubmissionIntakeService intake,
        SubmissionMaterializer materializer, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _intake = intake;
        _materializer = materializer;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/motions");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(MotionCreateRequest req, CancellationToken ct) {
        if (_chapterRepo.Get(req.ChapterId) == null) {
            AddError(r => r.ChapterId, "Die gewählte Gliederung existiert nicht.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        if (_perms.UserId != null) {
            var motionId = _materializer.MaterializeMotionDirect(req);
            await SendAsync(new SubmissionAcceptedResponse {
                Email = req.AuthorEmail,
                RequiresConfirmation = false,
                CreatedEntityId = motionId
            }, cancellation: ct);
            return;
        }

        await _intake.AcceptAsync(PendingSubmissionKind.Motion, req, req.AuthorEmail, ct);
        await SendAsync(new SubmissionAcceptedResponse { Email = req.AuthorEmail }, cancellation: ct);
    }
}
