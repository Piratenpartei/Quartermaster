using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.DueSelector;
using Quartermaster.Server.Forwarding;

namespace Quartermaster.Server.Admin;

public class DueSelectionProcessRequest {
    public Guid Id { get; set; }
    public DueSelectionStatus Status { get; set; }
}

public class DueSelectionProcessEndpoint : Endpoint<DueSelectionProcessRequest> {
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly DueSelectionMailService _mailService;
    private readonly ForwardingOutcomeHook _outcomeHook;
    private readonly PermissionContext _perms;

    public DueSelectionProcessEndpoint(DueSelectionRepository dueSelectionRepo,
        MembershipApplicationRepository applicationRepo, DueSelectionMailService mailService,
        ForwardingOutcomeHook outcomeHook, PermissionContext perms) {
        _dueSelectionRepo = dueSelectionRepo;
        _applicationRepo = applicationRepo;
        _mailService = mailService;
        _outcomeHook = outcomeHook;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/admin/dueselections/process");
    }

    public override async Task HandleAsync(DueSelectionProcessRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var selection = _dueSelectionRepo.Get(req.Id);
        if (selection == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        // The deciding chapter owns the selection; older rows without one belong to their application's chapter.
        var chapterId = selection.ChapterId ?? _applicationRepo.GetByDueSelectionId(selection.Id)?.ChapterId;
        if (chapterId.HasValue) {
            if (!_perms.Has(chapterId.Value, PermissionIdentifier.ProcessDueSelections)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!_perms.HasGlobal(PermissionIdentifier.ProcessDueSelections)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        if (req.Status != DueSelectionStatus.Approved && req.Status != DueSelectionStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }
        // A second decision would send the member a contradicting mail; selections decided in Redmine are decided there.
        if (selection.Status != DueSelectionStatus.Pending) {
            AddError(I18nKey.Error.Admin.DueSelection.AlreadyDecided);
            await SendErrorsAsync(409, ct);
            return;
        }

        _dueSelectionRepo.UpdateStatus(req.Id, req.Status, null);
        _outcomeHook.OnRecordDecided(ForwardingCaseKind.DueSelection, req.Id, DateTime.UtcNow);

        var updated = _dueSelectionRepo.Get(req.Id);
        if (updated != null) {
            await _mailService.SendDueSelectionDecisionAsync(updated, ct);
        }

        await SendOkAsync(ct);
    }
}
