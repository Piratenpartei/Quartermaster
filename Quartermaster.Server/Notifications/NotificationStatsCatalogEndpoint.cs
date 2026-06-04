using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Notifications;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Notifications;

public class NotificationStatsCatalogEndpoint : EndpointWithoutRequest<NotificationStatsCatalogDTO> {
    private readonly NotificationLogRepository _logRepo;
    private readonly TemplateRepository _templateRepo;
    private readonly PermissionContext _perms;

    public NotificationStatsCatalogEndpoint(NotificationLogRepository logRepo, TemplateRepository templateRepo, PermissionContext perms) {
        _logRepo = logRepo;
        _templateRepo = templateRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/notifications/stats/catalog");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewNotificationLogs)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var channels = NotificationChannelCatalog.All
            .Select(c => new NotificationStatsFilterOptionDTO {
                Value = c.ChannelId,
                DisplayName = c.DisplayName
            })
            .ToList();

        var triggers = NotificationTriggerCatalog.All
            .Select(t => new NotificationStatsFilterOptionDTO {
                Value = t.TriggerId,
                DisplayName = t.DisplayName
            })
            .ToList();

        var templates = _templateRepo.GetAll()
            .Where(t => !string.IsNullOrEmpty(t.Identifier))
            .Select(t => new NotificationStatsFilterOptionDTO {
                Value = t.Identifier!,
                DisplayName = t.DisplayName
            })
            .DistinctBy(o => o.Value)
            .OrderBy(o => o.DisplayName)
            .ToList();

        var sourceEntityTypes = _logRepo.GetDistinctSourceEntityTypes()
            .Select(s => new NotificationStatsFilterOptionDTO {
                Value = s,
                DisplayName = s
            })
            .ToList();

        await SendAsync(new NotificationStatsCatalogDTO {
            Channels = channels,
            Triggers = triggers,
            Templates = templates,
            SourceEntityTypes = sourceEntityTypes
        }, cancellation: ct);
    }
}
