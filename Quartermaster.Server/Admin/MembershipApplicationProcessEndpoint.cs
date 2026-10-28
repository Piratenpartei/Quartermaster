using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.ApplicantRegister;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Forwarding.SageEntry;
using Quartermaster.Server.MembershipApplications;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationProcessRequest {
    public Guid Id { get; set; }
    public ApplicationStatus Status { get; set; }
}

public class MembershipApplicationProcessEndpoint : Endpoint<MembershipApplicationProcessRequest> {
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly MembershipApplicationMailService _mailService;
    private readonly ForwardingOutcomeHook _outcomeHook;
    private readonly SageEntryGate _gate;
    private readonly PermissionContext _perms;
    private readonly ChapterRepository _chapterRepo;
    private readonly MotionRepository _motionRepo;
    private readonly FederalApprovalFlow _federalApproval;

    public MembershipApplicationProcessEndpoint(MembershipApplicationRepository applicationRepo,
        MembershipApplicationMailService mailService, ForwardingOutcomeHook outcomeHook, SageEntryGate gate, PermissionContext perms,
        ChapterRepository chapterRepo, MotionRepository motionRepo, FederalApprovalFlow federalApproval) {
        _applicationRepo = applicationRepo;
        _mailService = mailService;
        _outcomeHook = outcomeHook;
        _gate = gate;
        _perms = perms;
        _chapterRepo = chapterRepo;
        _motionRepo = motionRepo;
        _federalApproval = federalApproval;
    }

    public override void Configure() {
        Post("/api/admin/membershipapplications/process");
    }

    public override async Task HandleAsync(MembershipApplicationProcessRequest req, CancellationToken ct) {
        var application = _applicationRepo.Get(req.Id);
        if (application == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var rootChapterId = _chapterRepo.GetRootChapter()?.Id;
        if (!MayProcess(application, rootChapterId)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (req.Status != ApplicationStatus.Approved && req.Status != ApplicationStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }
        // A second decision would send the applicant a contradicting mail.
        if (!application.Status.IsUndecided()) {
            AddError(I18nKey.Error.Admin.Application.AlreadyDecided);
            await SendErrorsAsync(409, ct);
            return;
        }
        // Without its chapter the application has no SAGE step yet, so an approval would mail before the SAGE entry.
        if (req.Status == ApplicationStatus.Approved && application.Status == ApplicationStatus.PendingDivisionLinking) {
            AddError(I18nKey.Error.Admin.Application.ChapterUnknown);
            await SendErrorsAsync(409, ct);
            return;
        }

        var status = req.Status == ApplicationStatus.Approved ? application.ApprovedStatus(rootChapterId) : req.Status;
        var decidedAtUtc = DateTime.UtcNow;
        if (!_applicationRepo.ClaimStatus(req.Id, application.Status, status, _perms.UserId)) {
            AddError(I18nKey.Error.Admin.Application.AlreadyDecided);
            await SendErrorsAsync(409, ct);
            return;
        }
        await _federalApproval.OnRecordDecidedAsync(req.Id, decidedAtUtc);
        CloseOpenMotions(req.Id);
        // The chapter's approval of an application that needs the federal one decides nothing yet; its forwarding steps keep waiting.
        if (status != ApplicationStatus.PendingFederalApproval)
            _outcomeHook.OnRecordDecided(ForwardingCaseKind.Application, req.Id, decidedAtUtc);

        var updated = _applicationRepo.Get(req.Id);
        if (updated != null && !_gate.HoldsApprovalMail(updated.Id)) {
            await _mailService.SendApplicationDecisionAsync(updated, ct);
        }

        await SendOkAsync(ct);
    }

    /// <summary>
    /// A motion left open after the dialog decided could still be voted on and mail the applicant a second decision. Closing it is no decision:
    /// the forwarding outcome falls back to the application's status. The federal motion just opened for the dialog's chapter approval stays open,
    /// and a motion resolved by a vote in the meantime keeps its result.
    /// </summary>
    private void CloseOpenMotions(Guid applicationId) {
        var application = _applicationRepo.Get(applicationId);
        var keepOpenId = application?.Status == ApplicationStatus.PendingFederalApproval ? application.FederalApprovalMotionId : null;
        foreach (var motion in _motionRepo.ListByLinkedApplicationId(applicationId)) {
            if (motion.ApprovalStatus == MotionApprovalStatus.Pending && motion.Id != keepOpenId)
                _motionRepo.UpdatePendingApprovalStatus(motion.Id, MotionApprovalStatus.ClosedWithoutAction);
        }
    }

    /// <summary>The federal step is decided with the root chapter's permission, never the chapter's own.</summary>
    private bool MayProcess(MembershipApplication application, Guid? rootChapterId) {
        if (application.Status == ApplicationStatus.PendingFederalApproval && rootChapterId != null)
            return _perms.Has(rootChapterId.Value, PermissionIdentifier.ProcessApplications);
        if (application.ChapterId.HasValue)
            return _perms.Has(application.ChapterId.Value, PermissionIdentifier.ProcessApplications);
        return _perms.HasGlobal(PermissionIdentifier.ProcessApplications);
    }
}
