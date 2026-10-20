using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Forwarding.Redmine;

namespace Quartermaster.Server.Forwarding.Endpoints;

/// <summary>Projects, trackers, statuses and priorities for the target editor, read through the chapter's own or inherited connection.</summary>
public class RedmineLookupEndpoint : Endpoint<ForwardingChapterRequest, RedmineLookupDTO> {
    private readonly ForwardingSettingsResolver _resolver;
    private readonly IRedmineClient _redmine;
    private readonly PermissionContext _perms;

    public RedmineLookupEndpoint(ForwardingSettingsResolver resolver, IRedmineClient redmine, PermissionContext perms) {
        _resolver = resolver;
        _redmine = redmine;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forwarding/redmine-lookups/{ChapterId}");
    }

    public override async Task HandleAsync(ForwardingChapterRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var connection = _resolver.ConnectionFor(req.ChapterId);
        if (connection == null) {
            AddError(I18nKey.Error.Forwarding.RedmineConnectionMissing);
            await SendErrorsAsync(409, ct);
            return;
        }

        var settings = new RedmineConnectionSettings(connection.BaseUrl, connection.ApiKey);
        try {
            await SendAsync(new RedmineLookupDTO {
                Projects = Map(await _redmine.ListProjectsAsync(settings, ct)),
                Trackers = Map(await _redmine.ListTrackersAsync(settings, ct)),
                Statuses = Map(await _redmine.ListIssueStatusesAsync(settings, ct)),
                Priorities = Map(await _redmine.ListIssuePrioritiesAsync(settings, ct))
            }, cancellation: ct);
        } catch (RedmineException ex) {
            AddError(ex.Message);
            await SendErrorsAsync(502, ct);
        }
    }

    private static List<IdNameDTO> Map(List<RedmineIdName> items) {
        return items.Select(i => new IdNameDTO { Id = i.Id, Name = i.Name }).ToList();
    }
}
