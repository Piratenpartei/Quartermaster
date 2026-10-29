using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.Chapters;

/// <summary>
/// Updates an existing chapter. Requires the global <see cref="PermissionIdentifier.EditChapter"/>
/// permission. Cannot set a chapter as its own ancestor (no cycle detection at depth - the
/// shallow self-reference check is enough for the current UX where re-parenting happens
/// one step at a time).
/// </summary>
public class ChapterUpdateEndpoint : Endpoint<ChapterUpdateEndpoint.Request, ChapterDTO> {
    public class Request : ChapterUpdateRequest {
        public Guid Id { get; set; }
    }

    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterUpdateEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/chapters/{Id}");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditChapter)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var chapter = _chapterRepo.Get(req.Id);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        var name = req.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) {
            AddError(r => r.Name, I18nKey.Error.Chapter.NameRequired);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (req.ParentChapterId == req.Id) {
            AddError(r => r.ParentChapterId, I18nKey.Error.Chapter.ParentSelfReference);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (req.ParentChapterId.HasValue) {
            var parent = _chapterRepo.Get(req.ParentChapterId.Value);
            if (parent == null) {
                AddError(r => r.ParentChapterId, I18nKey.Error.Chapter.ParentNotFound);
                await SendErrorsAsync(cancellation: ct);
                return;
            }
        }

        if (!Chapter.IsValidLevel(req.Level)) {
            AddError(r => r.Level, I18nKey.Error.Chapter.LevelInvalid);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (req.Level == ChapterLevel.Federal && req.ParentChapterId.HasValue) {
            AddError(r => r.Level, I18nKey.Error.Chapter.FederalRootOnly);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (!Chapter.IsValidTakeOverPeriod(req.TakeOverPeriodDays)) {
            AddError(r => r.TakeOverPeriodDays, I18nKey.Error.Chapter.TakeOverPeriodRange);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var externalCodes = NormaliseCodes(req.ExternalCodes);
        var conflict = _chapterRepo.FindCodeConflictUnderParent(externalCodes, req.ParentChapterId, excludeChapterId: req.Id);
        if (conflict != null) {
            AddError(r => r.ExternalCodes, I18nKey.Error.Chapter.ExternalCodeNotUnique);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var keycloakDepartment = string.IsNullOrWhiteSpace(req.KeycloakDepartment) ? null : req.KeycloakDepartment.Trim();
        if (keycloakDepartment?.Length > Chapter.KeycloakDepartmentMaxLength) {
            AddError(r => r.KeycloakDepartment, I18nKey.Error.Chapter.KeycloakDepartmentTooLong);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (keycloakDepartment != null && _chapterRepo.GetByKeycloakDepartment(keycloakDepartment, req.Id) != null) {
            AddError(r => r.KeycloakDepartment, I18nKey.Error.Chapter.KeycloakDepartmentNotUnique);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var contactEmail = string.IsNullOrWhiteSpace(req.ContactEmail) ? null : req.ContactEmail.Trim();
        if (contactEmail?.Length > Chapter.ContactEmailMaxLength) {
            AddError(r => r.ContactEmail, I18nKey.Error.Chapter.ContactEmailTooLong);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (contactEmail != null && !EmailAddressFormat.IsPlausible(contactEmail)) {
            AddError(r => r.ContactEmail, I18nKey.Error.Chapter.ContactEmailInvalid);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var divisionIds = req.AdministrativeDivisionIds.Distinct().ToList();

        chapter.Name = name;
        chapter.ShortCode = string.IsNullOrWhiteSpace(req.ShortCode) ? null : req.ShortCode.Trim();
        chapter.KeycloakDepartment = keycloakDepartment;
        chapter.ContactEmail = contactEmail;
        chapter.ParentChapterId = req.ParentChapterId;
        chapter.Level = req.ParentChapterId.HasValue ? req.Level : ChapterLevel.Federal;
        chapter.TakeOverPeriodDays = req.TakeOverPeriodDays;
        _chapterRepo.Update(chapter);
        _chapterRepo.ReplaceExternalCodes(chapter.Id, externalCodes);
        _chapterRepo.ReplaceAdministrativeDivisions(chapter.Id, divisionIds);

        var dto = chapter.ToDto(externalCodes, divisionIds);
        dto.ContactEmail = chapter.ContactEmail;
        await SendAsync(dto, cancellation: ct);
    }

    private static List<string> NormaliseCodes(IEnumerable<string> raw)
        => raw.Select(c => c?.Trim() ?? "")
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();
}
