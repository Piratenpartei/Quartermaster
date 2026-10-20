using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Submissions;
using Quartermaster.Data.Members;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.PublicForms;

namespace Quartermaster.Server.DueSelector;

/// <summary>
/// Due-selection submission, for logged-in users only; it takes effect at once without an email confirmation. With a linked member record the person data
/// comes from that record, whatever the form sent. While the public form is switched off, only holders of dueselections_process may still submit.
/// </summary>
public class DueSelectionCreateEndpoint : Endpoint<DueSelectionDTO, SubmissionAcceptedResponse> {
    private readonly ForwardingIntake _intake;
    private readonly MemberRepository _members;
    private readonly UserRepository _users;
    private readonly PermissionContext _perms;
    private readonly PublicFormSettings _publicForms;

    public DueSelectionCreateEndpoint(ForwardingIntake intake, MemberRepository members, UserRepository users, PermissionContext perms, PublicFormSettings publicForms) {
        _intake = intake;
        _members = members;
        _users = users;
        _perms = perms;
        _publicForms = publicForms;
    }

    public override void Configure() {
        Post("/api/dueselector");
        Options(b => b.RequireRateLimiting(Program.DueSelectionRateLimitPolicy));
    }

    public override async Task HandleAsync(DueSelectionDTO req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_publicForms.IsDueSelectionEnabled() && !_perms.HasInAnyChapter(PermissionIdentifier.ProcessDueSelections))
            ThrowError(I18nKey.Error.PublicForm.Disabled, 403);

        var userId = _perms.UserId.Value;
        var user = _users.Get(userId);
        var member = _members.GetByUserIdIncludingFloating(userId);
        if (member != null) {
            // A floating member (accepted, no number yet) sends no number; the client's value is never trusted for a linked member.
            req.MemberNumber = member.MemberNumber ?? 0;
            req.FirstName = member.FirstName;
            req.LastName = member.LastName;
            req.Email = string.IsNullOrWhiteSpace(member.Email) ? user?.Email ?? req.Email : member.Email;
        }

        var userName = user == null ? null : user.Username ?? $"{user.FirstName} {user.LastName}".Trim();
        var result = _intake.IntakeDueSelection(req, member?.ChapterId, userId, userName, member == null, DateTime.UtcNow);
        await SendAsync(new SubmissionAcceptedResponse {
            Email = req.Email,
            RequiresConfirmation = false,
            CreatedEntityId = result.DueSelectionId ?? result.CaseId
        }, cancellation: ct);
    }
}
