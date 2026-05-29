using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Submissions;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Submissions;
#if DEBUG
using Quartermaster.Data.Options;
using Quartermaster.Server.Email;
#endif

namespace Quartermaster.Server.DueSelector;

/// <summary>
/// Due-selection submission. Authenticated callers create directly; anonymous callers go
/// through the email-confirm pending flow.
/// </summary>
public class DueSelectionCreateEndpoint : Endpoint<DueSelectionDTO, SubmissionAcceptedResponse> {
    private readonly SubmissionIntakeService _intake;
    private readonly SubmissionMaterializer _materializer;
    private readonly PermissionContext _perms;
#if DEBUG
    private readonly OptionRepository _optionRepo;
#endif

    public DueSelectionCreateEndpoint(SubmissionIntakeService intake,
        SubmissionMaterializer materializer, PermissionContext perms
#if DEBUG
        , OptionRepository optionRepo
#endif
    ) {
        _intake = intake;
        _materializer = materializer;
        _perms = perms;
#if DEBUG
        _optionRepo = optionRepo;
#endif
    }

    public override void Configure() {
        Post("/api/dueselector");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(DueSelectionDTO req, CancellationToken ct) {
        var skipConfirmation = false;
#if DEBUG
        skipConfirmation = SmtpConfig.ReadFrom(_optionRepo) == null;
#endif

        if (_perms.UserId != null || skipConfirmation) {
            var id = _materializer.MaterializeDueSelectionDirect(req);
            await SendAsync(new SubmissionAcceptedResponse {
                Email = req.Email,
                RequiresConfirmation = false,
                CreatedEntityId = id
            }, cancellation: ct);
            return;
        }

        await _intake.AcceptAsync(PendingSubmissionKind.DueSelection, req, req.Email, ct);
        await SendAsync(new SubmissionAcceptedResponse { Email = req.Email }, cancellation: ct);
    }
}
