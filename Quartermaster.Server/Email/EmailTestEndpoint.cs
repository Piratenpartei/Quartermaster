using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Email;
using Quartermaster.Api.I18n;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Email;

/// <summary>
/// Sends a synchronous SMTP test email so the setup page can verify configuration.
/// Requires the global <see cref="PermissionIdentifier.EditOptions"/> permission.
/// </summary>
public class EmailTestEndpoint : Endpoint<EmailTestRequest, EmailTestResultDTO> {
    private readonly SmtpTestService _testService;
    private readonly PermissionContext _perms;

    public EmailTestEndpoint(SmtpTestService testService, PermissionContext perms) {
        _testService = testService;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/email/test");
    }

    public override async Task HandleAsync(EmailTestRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (string.IsNullOrWhiteSpace(req.Recipient) || !req.Recipient.Contains('@')) {
            AddError(r => r.Recipient, I18nKey.Error.Email.Test.RecipientInvalid);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var error = await _testService.SendTestAsync(req.Recipient, ct);
        await SendAsync(new EmailTestResultDTO { Success = error == null, Error = error }, cancellation: ct);
    }
}
