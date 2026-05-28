using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Submissions;
using Quartermaster.Server.Submissions;

namespace Quartermaster.Server.Motions;

/// <summary>
/// Public motion submission. Validates and stashes the request as a pending submission,
/// then emails the author a confirmation link — the motion is only created once they
/// confirm, so unconfirmed spam never reaches the live table or notifies officers.
/// </summary>
public class MotionCreateEndpoint : Endpoint<MotionCreateRequest, SubmissionAcceptedResponse> {
    private readonly ChapterRepository _chapterRepo;
    private readonly SubmissionIntakeService _intake;

    public MotionCreateEndpoint(ChapterRepository chapterRepo, SubmissionIntakeService intake) {
        _chapterRepo = chapterRepo;
        _intake = intake;
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

        await _intake.AcceptAsync(PendingSubmissionKind.Motion, req, req.AuthorEmail, ct);
        await SendAsync(new SubmissionAcceptedResponse { Email = req.AuthorEmail }, cancellation: ct);
    }
}
