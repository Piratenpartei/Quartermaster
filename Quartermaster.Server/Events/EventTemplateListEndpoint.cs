using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventTemplateListEndpoint : EndpointWithoutRequest<List<EventTemplateDTO>> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public EventTemplateListEndpoint(
        EventRepository eventRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Get("/api/eventtemplates");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var allowedChapterIds = EndpointAuthorizationHelper.GetPermittedChapterIds(
            userId.Value, PermissionIdentifier.ViewTemplates, _globalPermRepo, _chapterPermRepo, _chapterRepo);
        if (allowedChapterIds is { Count: 0 }) {
            await SendForbiddenAsync(ct);
            return;
        }

        var templates = _eventRepo.GetAllTemplates(allowedChapterIds);

        var dtos = templates.Select(t => {
            var variableCount = 0;
            var checklistItemCount = 0;

            try {
                var variables = JsonSerializer.Deserialize<JsonElement>(t.Variables);
                if (variables.ValueKind == JsonValueKind.Array)
                    variableCount = variables.GetArrayLength();
            }
            catch {
                // ignore malformed JSON
            }

            try {
                var items = JsonSerializer.Deserialize<JsonElement>(t.ChecklistItemTemplates);
                if (items.ValueKind == JsonValueKind.Array)
                    checklistItemCount = items.GetArrayLength();
            }
            catch {
                // ignore malformed JSON
            }

            return new EventTemplateDTO {
                Id = t.Id,
                Name = t.Name,
                VariableCount = variableCount,
                ChecklistItemCount = checklistItemCount,
                ChapterId = t.ChapterId,
                CreatedAt = t.CreatedAt
            };
        }).ToList();

        await SendAsync(dtos, cancellation: ct);
    }
}
