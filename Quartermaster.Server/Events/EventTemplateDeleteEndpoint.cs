using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateDeleteRequest {
    public Guid Id { get; set; }
}

public class EventTemplateDeleteEndpoint : Endpoint<EventTemplateDeleteRequest> {
    private readonly EventRepository _eventRepo;

    public EventTemplateDeleteEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Delete("/api/eventtemplates/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventTemplateDeleteRequest req, CancellationToken ct) {
        _eventRepo.DeleteTemplate(req.Id);
        await SendOkAsync(ct);
    }
}
