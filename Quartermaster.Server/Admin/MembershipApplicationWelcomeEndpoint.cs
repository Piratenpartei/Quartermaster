using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.MembershipApplications;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationWelcomeRequest {
    public Guid Id { get; set; }
    public int MemberNumber { get; set; }
}

/// <summary>
/// Manually assigns a member number to an approved application and sends the applicant the
/// welcome mail carrying that number. Single-use per application (guarded by WelcomeSentAt).
/// </summary>
public class MembershipApplicationWelcomeEndpoint : Endpoint<MembershipApplicationWelcomeRequest> {
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly MembershipApplicationMailService _mailService;
    private readonly PermissionContext _perms;

    public MembershipApplicationWelcomeEndpoint(
        MembershipApplicationRepository applicationRepo,
        MembershipApplicationMailService mailService,
        PermissionContext perms) {
        _applicationRepo = applicationRepo;
        _mailService = mailService;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/admin/membershipapplications/welcome");
    }

    public override async Task HandleAsync(MembershipApplicationWelcomeRequest req, CancellationToken ct) {
        var application = _applicationRepo.Get(req.Id);
        if (application == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (application.ChapterId.HasValue) {
            if (!_perms.Has(application.ChapterId.Value, PermissionIdentifier.ProcessApplications)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!_perms.HasGlobal(PermissionIdentifier.ProcessApplications)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        if (application.Status != ApplicationStatus.Approved) {
            AddError(r => r.Id, I18nKey.Error.Admin.Application.NotApprovedForWelcome);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        if (application.WelcomeSentAt != null) {
            AddError(r => r.Id, I18nKey.Error.Admin.Application.WelcomeAlreadySent);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        _applicationRepo.SetMemberNumberAndWelcome(application.Id, req.MemberNumber, DateTime.UtcNow);
        await _mailService.SendWelcomeAsync(application, req.MemberNumber, ct);
        await SendOkAsync(ct);
    }
}
