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
/// Creates a new chapter, optionally under a parent. Requires the global
/// <see cref="PermissionIdentifier.CreateChapter"/> permission. External codes must not
/// overlap with any sibling chapter's codes under the same parent.
/// </summary>
public class ChapterCreateEndpoint : Endpoint<ChapterCreateRequest, ChapterDTO> {
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterCreateEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/chapters");
    }

    public override async Task HandleAsync(ChapterCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.CreateChapter)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var name = req.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) {
            AddError(r => r.Name, I18nKey.Error.Chapter.NameRequired);
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
        var conflict = _chapterRepo.FindCodeConflictUnderParent(externalCodes, req.ParentChapterId);
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
        if (keycloakDepartment != null && _chapterRepo.GetByKeycloakDepartment(keycloakDepartment) != null) {
            AddError(r => r.KeycloakDepartment, I18nKey.Error.Chapter.KeycloakDepartmentNotUnique);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var divisionIds = req.AdministrativeDivisionIds.Distinct().ToList();

        var chapter = new Chapter {
            Id = Guid.NewGuid(),
            Name = name,
            ShortCode = string.IsNullOrWhiteSpace(req.ShortCode) ? null : req.ShortCode.Trim(),
            KeycloakDepartment = keycloakDepartment,
            ParentChapterId = req.ParentChapterId
        };
        _chapterRepo.Create(chapter);
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
