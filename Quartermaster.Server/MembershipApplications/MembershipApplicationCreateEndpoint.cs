using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Submissions;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Submissions;

namespace Quartermaster.Server.MembershipApplications;

/// <summary>
/// Membership-application submission. Authenticated callers (officers entering a paper
/// application on someone else's behalf) create directly; anonymous applicants go through
/// the email-confirm pending flow.
/// </summary>
public class MembershipApplicationCreateEndpoint : Endpoint<MembershipApplicationDTO, SubmissionAcceptedResponse> {
    private readonly SubmissionIntakeService _intake;
    private readonly SubmissionMaterializer _materializer;
    private readonly PermissionContext _perms;

    public MembershipApplicationCreateEndpoint(SubmissionIntakeService intake,
        SubmissionMaterializer materializer, PermissionContext perms) {
        _intake = intake;
        _materializer = materializer;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/membershipapplications");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(MembershipApplicationDTO req, CancellationToken ct) {
        if (_perms.UserId != null) {
            var id = await _materializer.MaterializeApplicationDirectAsync(req, ct);
            await SendAsync(new SubmissionAcceptedResponse {
                Email = req.Email,
                RequiresConfirmation = false,
                CreatedEntityId = id
            }, cancellation: ct);
            return;
        }

        await _intake.AcceptAsync(PendingSubmissionKind.MembershipApplication, req, req.Email, ct);
        await SendAsync(new SubmissionAcceptedResponse { Email = req.Email }, cancellation: ct);
    }
}
