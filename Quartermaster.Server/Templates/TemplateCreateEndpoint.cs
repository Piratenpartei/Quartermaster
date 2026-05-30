using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Templates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Templates;

public class TemplateCreateEndpoint : Endpoint<TemplateCreateRequest, TemplateListItemDTO> {
    private readonly TemplateRepository _templateRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public TemplateCreateEndpoint(TemplateRepository templateRepo, ChapterRepository chapterRepo,
        PermissionContext perms) {
        _templateRepo = templateRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/templates");
    }

    public override async Task HandleAsync(TemplateCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (req.ChapterId.HasValue) {
            if (_chapterRepo.Get(req.ChapterId.Value) == null) {
                await SendErrorsAsync(400, ct);
                return;
            }
            if (!_perms.Has(req.ChapterId.Value, PermissionIdentifier.EditTemplates)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else if (!_perms.HasGlobal(PermissionIdentifier.EditTemplates)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (string.IsNullOrWhiteSpace(req.DisplayName)) {
            await SendErrorsAsync(400, ct);
            return;
        }

        var template = new Template {
            Id = Guid.NewGuid(),
            Identifier = null,
            DisplayName = req.DisplayName.Trim(),
            IsSystem = false,
            ChapterId = req.ChapterId,
            Subject = req.Subject,
            Body = req.Body,
            AllowsMemberFields = req.AllowsMemberFields,
            AllowsEventFields = req.AllowsEventFields,
            AllowsChapterFields = req.AllowsChapterFields,
            CreatedAt = DateTime.UtcNow
        };
        _templateRepo.Create(template);

        await SendAsync(new TemplateListItemDTO {
            Id = template.Id,
            Identifier = template.Identifier,
            DisplayName = template.DisplayName,
            IsSystem = template.IsSystem,
            ChapterId = template.ChapterId,
            AllowsMemberFields = template.AllowsMemberFields,
            AllowsEventFields = template.AllowsEventFields,
            AllowsChapterFields = template.AllowsChapterFields
        }, cancellation: ct);
    }
}
