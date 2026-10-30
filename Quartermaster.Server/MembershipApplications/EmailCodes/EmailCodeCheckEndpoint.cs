using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Server.MembershipApplications.EmailCodes;

/// <summary>
/// Lets the application form verify the code just before the summary step. A correct check
/// extends the code's lifetime; the code is only consumed when the application is stored.
/// </summary>
public class EmailCodeCheckEndpoint : Endpoint<EmailCodeCheckRequest> {
    private readonly EmailCodeService _codes;
    private readonly EmailCodeFormGate _gate;

    public EmailCodeCheckEndpoint(EmailCodeService codes, EmailCodeFormGate gate) {
        _codes = codes;
        _gate = gate;
    }

    public override void Configure() {
        Post("/api/membershipapplications/email-code/check");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.EmailCodeCheckRateLimitPolicy));
    }

    public override async Task HandleAsync(EmailCodeCheckRequest req, CancellationToken ct) {
        var refusal = _gate.Refusal(req.Email);
        if (refusal != null)
            ThrowError(refusal.Value.Key, refusal.Value.Status);

        var result = _codes.Check(req.Email, req.Code, DateTime.UtcNow);
        if (result == EmailCodeCheckResult.Valid) {
            await SendNoContentAsync(ct);
            return;
        }

        AddError(EmailCodeErrors.KeyFor(result));
        await SendErrorsAsync(400, ct);
    }
}
