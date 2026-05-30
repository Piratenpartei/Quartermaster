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

public class TemplateDetailRequest {
    public Guid Id { get; set; }
}

public class TemplateDetailEndpoint : Endpoint<TemplateDetailRequest, TemplateDetailDTO> {
    private readonly TemplateRepository _templateRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public TemplateDetailEndpoint(TemplateRepository templateRepo, ChapterRepository chapterRepo,
        PermissionContext perms) {
        _templateRepo = templateRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/templates/{Id}");
    }

    public override async Task HandleAsync(TemplateDetailRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var template = _templateRepo.Get(req.Id);
        if (template == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!CanView(template)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id);

        var overrides = template.ChapterId == null
            ? _templateRepo.GetOverridesForBaseId(template.Id)
                .Where(o => o.ChapterId.HasValue && chapters.ContainsKey(o.ChapterId.Value))
                .Select(o => {
                    var ch = chapters[o.ChapterId!.Value];
                    return new TemplateOverrideDTO {
                        Id = o.Id,
                        ChapterId = ch.Id,
                        ChapterName = ch.Name,
                        ChapterShortCode = ch.ShortCode ?? "",
                        Subject = o.Subject,
                        Body = o.Body
                    };
                }).ToList()
            : new();

        var dto = new TemplateDetailDTO {
            Id = template.Id,
            Identifier = template.Identifier,
            DisplayName = template.DisplayName,
            IsSystem = template.IsSystem,
            ChapterId = template.ChapterId,
            BaseTemplateId = template.BaseTemplateId,
            ChapterName = template.ChapterId.HasValue && chapters.TryGetValue(template.ChapterId.Value, out var c)
                ? c.Name
                : null,
            Subject = template.Subject,
            Body = template.Body,
            AllowsMemberFields = template.AllowsMemberFields,
            AllowsEventFields = template.AllowsEventFields,
            AllowsChapterFields = template.AllowsChapterFields,
            Overrides = overrides
        };

        await SendAsync(dto, cancellation: ct);
    }

    private bool CanView(Template template) {
        if (template.ChapterId == null)
            return _perms.HasGlobal(PermissionIdentifier.ViewTemplates);
        return _perms.Has(template.ChapterId.Value, PermissionIdentifier.ViewTemplates);
    }
}
