using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventTemplateListEndpoint : EndpointWithoutRequest<List<EventTemplateDTO>> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public EventTemplateListEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/eventtemplates");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var allowedChapterIds = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewTemplates);
        if (allowedChapterIds is { Count: 0 }) {
            await SendForbiddenAsync(ct);
            return;
        }

        var templates = _eventRepo.GetAllTemplates(allowedChapterIds);

        var dtos = templates.Select(t => {
            var variableCount = 0;
            var checklistItemCount = 0;

            try {
                variableCount = EventConfigSerializer.ParseVariables(t.Variables).Count;
            } catch (JsonException ex) {
                Logger.LogWarning(ex, "Malformed Variables JSON on EventTemplate {Id}; counting as 0", t.Id);
            }

            try {
                checklistItemCount = EventConfigSerializer.ParseTemplates(t.ChecklistItemTemplates).Count;
            } catch (JsonException ex) {
                Logger.LogWarning(ex, "Malformed ChecklistItemTemplates JSON on EventTemplate {Id}; counting as 0", t.Id);
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
