using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Submissions;
using Quartermaster.Server.Submissions;

namespace Quartermaster.Server.MembershipApplications;

/// <summary>
/// Public membership-application submission. Stashes the request and emails a confirmation
/// link; the application (plus its linked due selection and approval motion) is created only
/// on confirmation.
/// </summary>
public class MembershipApplicationCreateEndpoint : Endpoint<MembershipApplicationDTO, SubmissionAcceptedResponse> {
    private readonly SubmissionIntakeService _intake;

    public MembershipApplicationCreateEndpoint(SubmissionIntakeService intake) {
        _intake = intake;
    }

    public override void Configure() {
        Post("/api/membershipapplications");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(MembershipApplicationDTO req, CancellationToken ct) {
        await _intake.AcceptAsync(PendingSubmissionKind.MembershipApplication, req, req.Email, ct);
        await SendAsync(new SubmissionAcceptedResponse { Email = req.Email }, cancellation: ct);
    }
}
