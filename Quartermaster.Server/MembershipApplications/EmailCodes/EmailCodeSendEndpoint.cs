using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
#if DEBUG
using Quartermaster.Data.Options;
using Quartermaster.Server.Email;
#endif

namespace Quartermaster.Server.MembershipApplications.EmailCodes;

/// <summary>
/// Sends the confirmation code that anonymous applicants must enter before their
/// membership application is stored. Sends are capped per IP by <see cref="Program.EmailCodeRateLimitPolicy"/>
/// and per address by <see cref="EmailCodeService"/>.
/// </summary>
public class EmailCodeSendEndpoint : Endpoint<EmailCodeSendRequest, EmailCodeSendResponse> {
    private readonly EmailCodeService _codes;
    private readonly EmailCodeMailService _mail;
    private readonly EmailCodeFormGate _gate;
    private readonly ChapterRepository _chapterRepo;
#if DEBUG
    private readonly OptionRepository _optionRepo;
#endif

    public EmailCodeSendEndpoint(EmailCodeService codes, EmailCodeMailService mail, EmailCodeFormGate gate,
        ChapterRepository chapterRepo
#if DEBUG
        , OptionRepository optionRepo
#endif
    ) {
        _codes = codes;
        _mail = mail;
        _gate = gate;
        _chapterRepo = chapterRepo;
#if DEBUG
        _optionRepo = optionRepo;
#endif
    }

    public override void Configure() {
        Post("/api/membershipapplications/email-code");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.EmailCodeRateLimitPolicy));
    }

    public override async Task HandleAsync(EmailCodeSendRequest req, CancellationToken ct) {
        var refusal = _gate.Refusal(req.Email);
        if (refusal != null)
            ThrowError(refusal.Value.Key, refusal.Value.Status);

        if (req.ChapterId.HasValue && _chapterRepo.Get(req.ChapterId.Value) == null)
            ThrowError(I18nKey.Error.EmailCode.ChapterNotFound, 400);

        var skipCode = false;
#if DEBUG
        skipCode = SmtpConfig.ReadFrom(_optionRepo) == null;
#endif
        if (skipCode) {
            await SendAsync(new EmailCodeSendResponse { CodeRequired = false, LifetimeMinutes = _codes.LifetimeMinutes() }, cancellation: ct);
            return;
        }

        var issued = _codes.Issue(req.Email, DateTime.UtcNow);
        if (issued.LimitReached || issued.Code == null) {
            AddError(I18nKey.Error.EmailCode.TooManySends);
            await SendErrorsAsync(429, ct);
            return;
        }

        var minutes = _codes.LifetimeMinutes();
        await _mail.SendAsync(req.Email.Trim(), req.ChapterId, issued.Code, minutes, ct);
        await SendAsync(new EmailCodeSendResponse { CodeRequired = true, LifetimeMinutes = minutes }, cancellation: ct);
    }
}
