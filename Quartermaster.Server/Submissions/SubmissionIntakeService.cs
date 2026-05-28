using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Data.Submissions;

namespace Quartermaster.Server.Submissions;

/// <summary>
/// Entry point for public submissions: stashes the request as a <see cref="PendingSubmission"/>
/// and emails the submitter a confirmation link. The real entity is only created once the
/// submitter confirms (<see cref="SubmissionMaterializer"/>).
/// </summary>
public class SubmissionIntakeService {
    private readonly PendingSubmissionRepository _pendingRepo;
    private readonly SubmissionConfirmationEmailService _confirmationEmail;

    public SubmissionIntakeService(
        PendingSubmissionRepository pendingRepo,
        SubmissionConfirmationEmailService confirmationEmail) {
        _pendingRepo = pendingRepo;
        _confirmationEmail = confirmationEmail;
    }

    public async Task AcceptAsync(PendingSubmissionKind kind, object request, string email, CancellationToken ct) {
        var json = JsonSerializer.Serialize(request, request.GetType());
        var pending = _pendingRepo.Create(kind, json, email, DateTime.UtcNow);
        await _confirmationEmail.SendAsync(kind, request, pending.Token, email, ct);
    }
}
