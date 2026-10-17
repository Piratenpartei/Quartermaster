using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Submissions;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.PublicForms;
using Quartermaster.Server.Submissions;
#if DEBUG
using Quartermaster.Data.Options;
using Quartermaster.Server.Email;
#endif

namespace Quartermaster.Server.Motions;

/// <summary>
/// Motion submission. Authenticated callers create the motion directly (already-trusted
/// origin, no spam barrier). Anonymous callers go through the email-confirm pending flow:
/// the motion is only materialized once they click the link, so unconfirmed spam never
/// reaches the live table or pings officers. The public form must be switched on for the chapter; holders of
/// motions_edit (the board's own create page) are exempt.
/// </summary>
public class MotionCreateEndpoint : Endpoint<MotionCreateRequest, SubmissionAcceptedResponse> {
    private readonly ChapterRepository _chapterRepo;
    private readonly SubmissionIntakeService _intake;
    private readonly SubmissionMaterializer _materializer;
    private readonly PermissionContext _perms;
    private readonly PublicFormSettings _publicForms;
#if DEBUG
    private readonly OptionRepository _optionRepo;
#endif

    public MotionCreateEndpoint(ChapterRepository chapterRepo, SubmissionIntakeService intake,
        SubmissionMaterializer materializer, PermissionContext perms, PublicFormSettings publicForms
#if DEBUG
        , OptionRepository optionRepo
#endif
    ) {
        _chapterRepo = chapterRepo;
        _intake = intake;
        _materializer = materializer;
        _perms = perms;
        _publicForms = publicForms;
#if DEBUG
        _optionRepo = optionRepo;
#endif
    }

    public override void Configure() {
        Post("/api/motions");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(MotionCreateRequest req, CancellationToken ct) {
        var chapter = _chapterRepo.Get(req.ChapterId);
        if (chapter == null) {
            AddError(r => r.ChapterId, I18nKey.Error.Motion.ChapterNotFound);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (chapter.ArchivedAt.HasValue) {
            AddError(r => r.ChapterId, I18nKey.Error.Chapter.Archived);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        if (!_publicForms.IsMotionFormEnabled(chapter.Id) && !_perms.Has(chapter.Id, PermissionIdentifier.EditMotions)) {
            AddError(r => r.ChapterId, I18nKey.Error.Motion.PublicFormDisabled);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var skipConfirmation = false;
#if DEBUG
        skipConfirmation = SmtpConfig.ReadFrom(_optionRepo) == null;
#endif

        if (_perms.UserId != null || skipConfirmation) {
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
