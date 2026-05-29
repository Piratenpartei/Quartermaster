using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Rendering;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Motions;

/// <summary>
/// Substantive edit to an existing motion. Gated by <see cref="PermissionIdentifier.EditMotions"/>
/// on the motion's chapter and locked once <see cref="MotionApprovalStatus.Pending"/> is left —
/// resolved/closed motions are immutable for accountability. Field-level audit via
/// <see cref="MotionRepository.Update"/>.
/// </summary>
public class MotionUpdateEndpoint : Endpoint<MotionUpdateRequest> {
    private readonly MotionRepository _motionRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly PermissionContext _perms;

    public MotionUpdateEndpoint(MotionRepository motionRepo,
        MembershipApplicationRepository applicationRepo,
        DueSelectionRepository dueSelectionRepo,
        PermissionContext perms) {
        _motionRepo = motionRepo;
        _applicationRepo = applicationRepo;
        _dueSelectionRepo = dueSelectionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/motions/{Id}");
    }

    public override async Task HandleAsync(MotionUpdateRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.Id);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(motion.ChapterId, PermissionIdentifier.EditMotions)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (motion.ApprovalStatus != MotionApprovalStatus.Pending) {
            AddError("Antrag kann nach Beschluss nicht mehr bearbeitet werden.");
            await SendErrorsAsync(409, ct);
            return;
        }

        if (req.LinkedMembershipApplicationId.HasValue
            && _applicationRepo.Get(req.LinkedMembershipApplicationId.Value) == null) {
            AddError(r => r.LinkedMembershipApplicationId, "Verknüpfter Mitgliedsantrag existiert nicht.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (req.LinkedDueSelectionId.HasValue
            && _dueSelectionRepo.Get(req.LinkedDueSelectionId.Value) == null) {
            AddError(r => r.LinkedDueSelectionId, "Verknüpfte Beitragseinstufung existiert nicht.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var html = MarkdownService.ToHtml(req.TextMarkdown, SanitizationProfile.Strict);
        _motionRepo.Update(
            req.Id,
            req.Title,
            req.TextMarkdown,
            html,
            req.AuthorName,
            req.AuthorEmail,
            req.LinkedMembershipApplicationId,
            req.LinkedDueSelectionId);

        await SendOkAsync(ct);
    }
}
