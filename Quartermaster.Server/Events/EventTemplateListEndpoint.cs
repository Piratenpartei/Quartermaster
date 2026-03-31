using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateListEndpoint : EndpointWithoutRequest<List<EventTemplateDTO>> {
    private readonly EventRepository _eventRepo;

    public EventTemplateListEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Get("/api/eventtemplates");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var templates = _eventRepo.GetAllTemplates();

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
