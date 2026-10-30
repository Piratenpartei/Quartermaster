using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Submissions;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.DueSelector;
using Quartermaster.Server.MembershipApplications.EmailCodes;
using Quartermaster.Server.PublicForms;
using Quartermaster.Server.Submissions;
#if DEBUG
using Quartermaster.Data.Options;
using Quartermaster.Server.Email;
#endif

namespace Quartermaster.Server.MembershipApplications;

/// <summary>
/// Membership-application submission. Authenticated callers (officers entering a paper
/// application on someone else's behalf) create directly; anonymous applicants must enter the
/// code mailed to their address first. While the public form is switched off, only holders of applications_process
/// may still submit.
/// </summary>
public class MembershipApplicationCreateEndpoint : Endpoint<MembershipApplicationDTO, SubmissionAcceptedResponse> {
    private readonly EmailCodeService _codes;
    private readonly SubmissionMaterializer _materializer;
    private readonly PermissionContext _perms;
    private readonly PublicFormSettings _publicForms;
#if DEBUG
    private readonly OptionRepository _optionRepo;
#endif

    public MembershipApplicationCreateEndpoint(EmailCodeService codes,
        SubmissionMaterializer materializer, PermissionContext perms, PublicFormSettings publicForms
#if DEBUG
        , OptionRepository optionRepo
#endif
    ) {
        _codes = codes;
        _materializer = materializer;
        _perms = perms;
        _publicForms = publicForms;
#if DEBUG
        _optionRepo = optionRepo;
#endif
    }

    public override void Configure() {
        Post("/api/membershipapplications");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(MembershipApplicationDTO req, CancellationToken ct) {
        if (!_publicForms.IsMembershipApplicationReady())
            ThrowError(I18nKey.Error.Admin.Application.NotSetUp, 400);

        if (!_publicForms.IsMembershipApplicationEnabled() && !_perms.HasInAnyChapter(PermissionIdentifier.ProcessApplications))
            ThrowError(I18nKey.Error.PublicForm.Disabled, 403);

        if (req.DueSelection != null) {
            var reductionProblem = ReducedDueRules.Problem(req.DueSelection, _publicForms.ReducedDueMaximum(), _publicForms.IsPermanentReductionEnabled());
            if (reductionProblem != null)
                ThrowError(reductionProblem);
            ReducedDueRules.ApplyRejectionDefault(req.DueSelection);
        }

        var skipCode = false;
#if DEBUG
        skipCode = SmtpConfig.ReadFrom(_optionRepo) == null;
#endif

        var anonymous = _perms.UserId == null && !skipCode;
        if (anonymous) {
            if (string.IsNullOrWhiteSpace(req.EmailVerificationCode))
                ThrowError(I18nKey.Error.EmailCode.Required, 400);

            var result = _codes.Check(req.Email, req.EmailVerificationCode, DateTime.UtcNow);
            if (result != EmailCodeCheckResult.Valid)
                ThrowError(EmailCodeErrors.KeyFor(result), 400);
        }

        var id = await _materializer.MaterializeApplicationDirectAsync(req, ct);
        if (anonymous)
            _codes.Consume(req.Email);

        await SendAsync(new SubmissionAcceptedResponse {
            Email = req.Email,
            RequiresConfirmation = false,
            CreatedEntityId = id
        }, cancellation: ct);
    }
}
