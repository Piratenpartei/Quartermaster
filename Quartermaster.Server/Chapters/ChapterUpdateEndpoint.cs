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

namespace Quartermaster.Server.Chapters;

/// <summary>
/// Updates an existing chapter. Requires the global <see cref="PermissionIdentifier.EditChapter"/>
/// permission. Cannot set a chapter as its own ancestor (no cycle detection at depth — the
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

        var divisionIds = req.AdministrativeDivisionIds.Distinct().ToList();

        chapter.Name = name;
        chapter.ShortCode = string.IsNullOrWhiteSpace(req.ShortCode) ? null : req.ShortCode.Trim();
        chapter.KeycloakDepartment = keycloakDepartment;
        chapter.ParentChapterId = req.ParentChapterId;
        _chapterRepo.Update(chapter);
        _chapterRepo.ReplaceExternalCodes(chapter.Id, externalCodes);
        _chapterRepo.ReplaceAdministrativeDivisions(chapter.Id, divisionIds);

        await SendAsync(chapter.ToDto(externalCodes, divisionIds), cancellation: ct);
    }

    private static List<string> NormaliseCodes(IEnumerable<string> raw)
        => raw.Select(c => c?.Trim() ?? "")
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();
}
