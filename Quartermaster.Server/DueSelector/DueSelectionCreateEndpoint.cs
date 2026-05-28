using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Submissions;
using Quartermaster.Server.Submissions;

namespace Quartermaster.Server.DueSelector;

/// <summary>
/// Public due-selection submission. Stashes the request and emails a confirmation link;
/// the real due selection is created only on confirmation.
/// </summary>
public class DueSelectionCreateEndpoint : Endpoint<DueSelectionDTO, SubmissionAcceptedResponse> {
    private readonly SubmissionIntakeService _intake;

    public DueSelectionCreateEndpoint(SubmissionIntakeService intake) {
        _intake = intake;
    }

    public override void Configure() {
        Post("/api/dueselector");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(DueSelectionDTO req, CancellationToken ct) {
        await _intake.AcceptAsync(PendingSubmissionKind.DueSelection, req, req.Email, ct);
        await SendAsync(new SubmissionAcceptedResponse { Email = req.Email }, cancellation: ct);
    }
}
