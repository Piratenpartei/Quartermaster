using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Templates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Templates;

public class TemplateOverrideUpsertEndpoint : Endpoint<TemplateOverrideUpsertRequest, TemplateOverrideDTO> {
    private readonly TemplateRepository _templateRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public TemplateOverrideUpsertEndpoint(TemplateRepository templateRepo, ChapterRepository chapterRepo,
        PermissionContext perms) {
        _templateRepo = templateRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/templates/{TemplateId}/overrides");
    }

    public override async Task HandleAsync(TemplateOverrideUpsertRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var baseTemplate = _templateRepo.Get(req.TemplateId);
        if (baseTemplate == null || baseTemplate.ChapterId != null) {
            await SendNotFoundAsync(ct);
            return;
        }
        var chapter = _chapterRepo.Get(req.ChapterId);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_perms.Has(req.ChapterId, PermissionIdentifier.EditTemplates)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var existing = _templateRepo.GetOverridesForBaseId(baseTemplate.Id)
            .FirstOrDefault(t => t.ChapterId == req.ChapterId);

        Template saved;
        if (existing != null) {
            existing.Subject = req.Subject;
            existing.Body = req.Body;
            _templateRepo.Update(existing);
            saved = existing;
        } else {
            saved = new Template {
                Id = Guid.NewGuid(),
                Identifier = baseTemplate.Identifier,
                DisplayName = baseTemplate.DisplayName,
                IsSystem = baseTemplate.IsSystem,
                ChapterId = req.ChapterId,
                BaseTemplateId = baseTemplate.Id,
                Subject = req.Subject,
                Body = req.Body,
                AllowsMemberFields = baseTemplate.AllowsMemberFields,
                AllowsEventFields = baseTemplate.AllowsEventFields,
                AllowsChapterFields = baseTemplate.AllowsChapterFields,
                CreatedAt = DateTime.UtcNow
            };
            _templateRepo.Create(saved);
        }

        await SendAsync(new TemplateOverrideDTO {
            Id = saved.Id,
            ChapterId = chapter.Id,
            ChapterName = chapter.Name,
            ChapterShortCode = chapter.ShortCode ?? "",
            Subject = saved.Subject,
            Body = saved.Body
        }, cancellation: ct);
    }
}
