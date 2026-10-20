using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Rendering;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forwarding.Endpoints;

/// <summary>
/// Saves a chapter's forwarding settings and replaces its targets. A Redmine setting needs a resolvable connection and the target it uses;
/// reduction settings exist only for the federal chapter and state chapters.
/// </summary>
public class ForwardingChapterSaveEndpoint : Endpoint<ForwardingChapterSaveRequest> {
    private const int MaxTemplateLength = 1000;

    private readonly ForwardingSettingsRepository _settings;
    private readonly ForwardingSettingsResolver _resolver;
    private readonly ChapterRepository _chapters;
    private readonly AuditLogRepository _auditLog;
    private readonly PermissionContext _perms;

    public ForwardingChapterSaveEndpoint(ForwardingSettingsRepository settings, ForwardingSettingsResolver resolver, ChapterRepository chapters,
        AuditLogRepository auditLog, PermissionContext perms) {
        _settings = settings;
        _resolver = resolver;
        _chapters = chapters;
        _auditLog = auditLog;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/forwarding/chapters/{ChapterId}");
    }

    public override async Task HandleAsync(ForwardingChapterSaveRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var chapter = _chapters.Get(req.ChapterId);
        if (chapter == null) {
            AddError(I18nKey.Error.Forwarding.ChapterNotFound);
            await SendErrorsAsync(404, ct);
            return;
        }

        var problem = Problem(chapter, req);
        if (problem != null) {
            AddError(problem);
            await SendErrorsAsync(400, ct);
            return;
        }

        var email = string.IsNullOrWhiteSpace(req.FailureEmail) ? null : req.FailureEmail.Trim();
        _settings.SaveChapter(
            new ForwardingChapterSetting { ChapterId = chapter.Id, ApplicationMode = req.ApplicationMode, ReductionMode = req.ReductionMode, FailureEmail = email },
            req.Targets.Select(t => ForwardingTarget.FromDto(chapter.Id, t)).ToList());
        _auditLog.Log("ForwardingChapterSetting", chapter.Id, "Saved", "Modes", null, $"{req.ApplicationMode}/{req.ReductionMode}");
        await SendOkAsync(ct);
    }

    private string? Problem(Chapter chapter, ForwardingChapterSaveRequest req) {
        if (!ListsArePresent(req))
            return I18nKey.Error.Forwarding.TargetInvalid;
        if (req.ReductionMode != null && !DecidesOnReductions(chapter))
            return I18nKey.Error.Forwarding.ReductionModeNotAllowed;
        if (!IsValidEmail(req.FailureEmail))
            return I18nKey.Error.Forwarding.FailureEmailInvalid;
        if (!TargetsAreValid(req))
            return I18nKey.Error.Forwarding.TargetInvalid;
        if (req.Targets.SelectMany(t => t.CustomFields).Any(f => !TemplateRenderer.IsValid(f.Template)))
            return I18nKey.Error.Forwarding.CustomFieldTemplateInvalid;
        if (NeedsRedmine(req) && _resolver.ConnectionFor(chapter.Id) == null)
            return I18nKey.Error.Forwarding.RedmineConnectionMissing;
        if (req.ApplicationMode == ForwardingMode.Redmine && !HasTarget(req, ForwardingPurpose.Application))
            return I18nKey.Error.Forwarding.TargetMissing;
        if (req.ReductionMode == ForwardingMode.Redmine && !HasTarget(req, ForwardingPurpose.Reduction))
            return I18nKey.Error.Forwarding.TargetMissing;

        return null;
    }

    private bool DecidesOnReductions(Chapter chapter) {
        if (chapter.ParentChapterId == null)
            return true;
        return _chapters.Get(chapter.ParentChapterId.Value)?.ParentChapterId == null;
    }

    private static bool NeedsRedmine(ForwardingChapterSaveRequest req) {
        return req.ApplicationMode == ForwardingMode.Redmine || req.ReductionMode == ForwardingMode.Redmine;
    }

    private static bool HasTarget(ForwardingChapterSaveRequest req, ForwardingPurpose purpose) {
        return req.Targets.Any(t => t.Purpose == purpose);
    }

    /// <summary>JSON can send null for any list or text; the defaults of the request types only cover missing ones.</summary>
    private static bool ListsArePresent(ForwardingChapterSaveRequest req) {
        if (req.Targets == null)
            return false;

        foreach (var target in req.Targets) {
            if (target?.CustomFields == null)
                return false;
            if (target.CustomFields.Any(f => f?.Template == null))
                return false;
        }

        return true;
    }

    private static bool TargetsAreValid(ForwardingChapterSaveRequest req) {
        if (req.Targets.Select(t => t.Purpose).Distinct().Count() != req.Targets.Count)
            return false;

        foreach (var target in req.Targets) {
            if (target.ProjectId <= 0 || target.TrackerId <= 0)
                return false;
            if (target.CustomFields.Any(f => f.FieldId <= 0 || f.Template.Length > MaxTemplateLength))
                return false;
        }

        return true;
    }

    private static bool IsValidEmail(string? email) {
        if (string.IsNullOrWhiteSpace(email))
            return true;

        var trimmed = email.Trim();
        if (trimmed.Length > ForwardingChapterSetting.FailureEmailMaxLength)
            return false;
        var at = trimmed.IndexOf('@');
        return at > 0 && at < trimmed.Length - 1 && !trimmed.Contains(' ');
    }
}
