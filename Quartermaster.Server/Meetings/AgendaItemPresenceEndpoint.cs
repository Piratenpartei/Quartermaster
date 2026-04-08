using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class AgendaItemPresenceRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
    public Guid UserId { get; set; }
    public bool Present { get; set; }
}

/// <summary>
/// Toggles an officer's presence on a Presence-type agenda item.
/// Stores the set of present user IDs as JSON in the agenda item's Resolution field.
/// </summary>
public class AgendaItemPresenceEndpoint : Endpoint<AgendaItemPresenceRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public AgendaItemPresenceEndpoint(
        MeetingRepository meetingRepo, AgendaItemRepository agendaRepo,
        ChapterRepository chapterRepo, UserChapterPermissionRepository chapterPermRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _chapterRepo = chapterRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/meetings/{MeetingId}/agenda/{ItemId}/presence");
    }

    public override async Task HandleAsync(AgendaItemPresenceRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.MeetingId);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (meeting.Status != MeetingStatus.InProgress) {
            ThrowError("Anwesenheit nur während laufender Sitzung erfassbar.");
            return;
        }
        var item = _agendaRepo.Get(req.ItemId);
        if (item == null || item.MeetingId != meeting.Id) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (item.ItemType != AgendaItemType.Presence) {
            ThrowError("TOP ist kein Anwesenheitspunkt.");
            return;
        }
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditMeetings, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionWithInheritance(userId.Value, meeting.ChapterId, PermissionIdentifier.EditMeetings, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        // Load current presence list from Resolution field (JSON array of user ID strings)
        var presentIds = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(item.Resolution)) {
            try {
                var parsed = JsonSerializer.Deserialize<List<string>>(item.Resolution);
                if (parsed != null)
                    presentIds = new HashSet<string>(parsed);
            } catch {
                // Corrupted — reset
            }
        }

        var userIdStr = req.UserId.ToString();
        if (req.Present)
            presentIds.Add(userIdStr);
        else
            presentIds.Remove(userIdStr);

        _agendaRepo.UpdateResolution(req.ItemId, JsonSerializer.Serialize(presentIds.ToList()));
        await SendOkAsync(ct);
    }
}
