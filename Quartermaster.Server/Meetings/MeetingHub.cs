using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Collab;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Options;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Real-time hub for meeting pages. Clients join a meeting's group to receive
/// broadcasts about votes, agenda transitions, status changes, and presence
/// updates, and to collaborate on agenda item notes via Yjs CRDT messages
/// that the server relays without inspection.
///
/// The hub allows anonymous connections (no <c>[Authorize]</c> attribute) so
/// that anyone can view a Public meeting's live notes in read-only mode. Every
/// method that mutates state (<c>SendUpdate</c>, <c>SaveSnapshot</c>) checks
/// for an authenticated user and appropriate permissions; anonymous users
/// are limited to <c>LoadDocument</c>, <c>JoinMeeting</c>, and the receive-only
/// message stream.
/// </summary>
public class MeetingHub : Hub {
    public const string GroupPrefix = "meeting:";
    public const string DocumentEntityType = "AgendaItem";

    // Tracks the last snapshot save time per agenda item across all connected
    // clients so multiple concurrent editors don't all hit the DB every tick.
    private static readonly ConcurrentDictionary<Guid, DateTime> _lastSnapshotSave = new();

    // Tracks the active per-user color assignment for each document (agenda
    // item). When a user joins we pick the first unused color from the
    // palette; they keep it for the lifetime of the server process. On
    // reconnect they get the same color if it's still in the map. This is
    // in-memory only — color assignment is ephemeral presence state.
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>> _documentColors = new();

    // 8-color Tol Vibrant scheme: colorblind-safe, distinguishable on both
    // light and dark backgrounds. Applied as cursor color and (via lower
    // alpha) as background tint in the per-character authorship layer.
    private static readonly string[] ColorPalette = new[] {
        "#EE7733", "#0077BB", "#33BBEE", "#EE3377",
        "#CC3311", "#009988", "#BBBBBB", "#000000"
    };

    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly RoleRepository _roleRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly CollabDocumentRepository _collabRepo;
    private readonly OptionRepository _optionRepo;

    public MeetingHub(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        RoleRepository roleRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        ChapterRepository chapterRepo,
        CollabDocumentRepository collabRepo,
        OptionRepository optionRepo) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _roleRepo = roleRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _chapterRepo = chapterRepo;
        _collabRepo = collabRepo;
        _optionRepo = optionRepo;
    }

    public static string GroupFor(Guid meetingId) => GroupPrefix + meetingId.ToString("N");
    public static string DocumentGroupFor(Guid agendaItemId) => "doc:" + agendaItemId.ToString("N");

    public async Task JoinMeeting(Guid meetingId) {
        var userId = EndpointAuthorizationHelper.GetUserId(Context.User!);

        var meeting = _meetingRepo.Get(meetingId);
        if (meeting == null)
            throw new HubException("meeting_not_found");

        if (!CanJoin(userId, meeting))
            throw new HubException("forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(meetingId));
    }

    public async Task LeaveMeeting(Guid meetingId) {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(meetingId));
    }

    /// <summary>
    /// Loads the initial collaborative document state for an agenda item. If
    /// no row exists yet, seeds one from the agenda item's current plain-text
    /// Notes column (empty state). The caller is also added to the
    /// per-agenda-item relay group so incoming updates get forwarded.
    /// </summary>
    public async Task<CollabDocumentSnapshot> LoadDocument(Guid agendaItemId) {
        var userId = EndpointAuthorizationHelper.GetUserId(Context.User!);

        var item = _agendaRepo.Get(agendaItemId);
        if (item == null)
            throw new HubException("agenda_item_not_found");

        var meeting = _meetingRepo.Get(item.MeetingId);
        if (meeting == null)
            throw new HubException("meeting_not_found");

        if (!CanJoin(userId, meeting))
            throw new HubException("forbidden");

        // Documents for meetings that are no longer in progress are frozen —
        // loading still works (so participants and archivers can view the
        // final state) but canEdit is false, which puts the client editor in
        // read-only mode. HasEditPermission already enforces the status check.
        // Anonymous users never get edit rights.
        var canEdit = userId.HasValue && HasEditPermission(userId.Value, meeting);

        await Groups.AddToGroupAsync(Context.ConnectionId, DocumentGroupFor(agendaItemId));

        var doc = _collabRepo.Get(DocumentEntityType, agendaItemId);
        var snapshot = new CollabDocumentSnapshot {
            SaveIntervalSeconds = ResolveSaveIntervalSeconds(),
            CanEdit = canEdit,
            // Anonymous users don't get a palette color since they can't
            // author anything — we return the first color as a placeholder
            // that never gets used for attribution.
            AssignedColor = userId.HasValue
                ? AssignColor(agendaItemId, userId.Value)
                : ColorPalette[0]
        };
        if (doc != null) {
            snapshot.DocumentStateBase64 = doc.DocumentState;
            snapshot.PlainText = doc.PlainText;
            snapshot.KnownAuthors = ParseKnownAuthors(doc.ClientUserMap);
        } else {
            // Seed from the legacy Notes column (still empty state for Yjs —
            // the client applies it as the initial text on first load).
            snapshot.PlainText = item.Notes ?? "";
        }
        return snapshot;
    }

    /// <summary>
    /// Returns the color for this user on this document. If the user already
    /// has one (e.g., they reconnected), returns it; otherwise picks the
    /// first unused color from the palette. If all 8 colors are taken, wraps
    /// around deterministically by user hash.
    /// </summary>
    private static string AssignColor(Guid agendaItemId, Guid userId) {
        var perDoc = _documentColors.GetOrAdd(agendaItemId, _ => new ConcurrentDictionary<Guid, string>());
        if (perDoc.TryGetValue(userId, out var existing))
            return existing;

        var used = new HashSet<string>(perDoc.Values);
        foreach (var candidate in ColorPalette) {
            if (!used.Contains(candidate)) {
                perDoc[userId] = candidate;
                return candidate;
            }
        }
        var fallback = ColorPalette[Math.Abs(userId.GetHashCode()) % ColorPalette.Length];
        perDoc[userId] = fallback;
        return fallback;
    }

    /// <summary>
    /// Relay an opaque Yjs update byte array to all other clients in the
    /// same agenda-item group. Permission check ensures read-only viewers
    /// can't poison the document state.
    /// </summary>
    public async Task SendUpdate(Guid agendaItemId, string updateBase64) {
        var userId = EndpointAuthorizationHelper.GetUserId(Context.User!);
        if (userId == null)
            throw new HubException("unauthenticated");

        var item = _agendaRepo.Get(agendaItemId);
        if (item == null)
            throw new HubException("agenda_item_not_found");

        var meeting = _meetingRepo.Get(item.MeetingId);
        if (meeting == null)
            throw new HubException("meeting_not_found");

        if (!HasEditPermission(userId.Value, meeting))
            throw new HubException("forbidden");

        await Clients.OthersInGroup(DocumentGroupFor(agendaItemId))
            .SendAsync(MeetingHubMethods.ReceiveUpdate, agendaItemId, updateBase64);
    }

    /// <summary>
    /// Relay an opaque Yjs awareness update (cursor positions, presence) to
    /// other clients. Awareness is ephemeral and requires only view access.
    /// </summary>
    public async Task SendAwareness(Guid agendaItemId, string awarenessBase64) {
        var userId = EndpointAuthorizationHelper.GetUserId(Context.User!);
        if (userId == null)
            throw new HubException("unauthenticated");

        var item = _agendaRepo.Get(agendaItemId);
        if (item == null)
            return;
        var meeting = _meetingRepo.Get(item.MeetingId);
        if (meeting == null)
            return;
        if (!CanJoin(userId.Value, meeting))
            return;

        await Clients.OthersInGroup(DocumentGroupFor(agendaItemId))
            .SendAsync(MeetingHubMethods.ReceiveAwareness, agendaItemId, awarenessBase64);
    }

    /// <summary>
    /// Persists a snapshot of the current document state. Throttled to at
    /// most once per save-interval window — if any client has already saved
    /// within the interval, subsequent calls in the same window are no-ops.
    /// Also writes the denormalized plain text back to the legacy
    /// AgendaItem.Notes column so existing read paths (PDF, audit log) keep
    /// working without needing to parse Yjs binary on the server.
    /// </summary>
    public async Task SaveSnapshot(CollabSnapshotRequest req) {
        var userId = EndpointAuthorizationHelper.GetUserId(Context.User!);
        if (userId == null)
            throw new HubException("unauthenticated");

        var item = _agendaRepo.Get(req.AgendaItemId);
        if (item == null)
            throw new HubException("agenda_item_not_found");

        var meeting = _meetingRepo.Get(item.MeetingId);
        if (meeting == null)
            throw new HubException("meeting_not_found");

        if (!HasEditPermission(userId.Value, meeting))
            throw new HubException("forbidden");

        var interval = TimeSpan.FromSeconds(ResolveSaveIntervalSeconds());
        var now = DateTime.UtcNow;
        var shouldSave = true;
        _lastSnapshotSave.AddOrUpdate(req.AgendaItemId,
            _ => now,
            (_, last) => {
                if (now - last < interval) {
                    shouldSave = false;
                    return last;
                }
                return now;
            });

        if (!shouldSave)
            return;

        // Merge the incoming KnownAuthors with any existing ones on disk so
        // a client that doesn't know about an old author can't accidentally
        // drop them from the persistent map.
        var merged = new Dictionary<string, CollabAuthorInfo>(req.KnownAuthors ?? new());
        var existing = _collabRepo.Get(DocumentEntityType, req.AgendaItemId);
        if (existing != null) {
            var existingAuthors = ParseKnownAuthors(existing.ClientUserMap);
            foreach (var kv in existingAuthors) {
                if (!merged.ContainsKey(kv.Key))
                    merged[kv.Key] = kv.Value;
            }
        }

        _collabRepo.Upsert(new CollabDocument {
            EntityType = DocumentEntityType,
            EntityId = req.AgendaItemId,
            DocumentState = req.DocumentStateBase64,
            PlainText = req.PlainText,
            ClientUserMap = SerializeKnownAuthors(merged),
            LastUpdatedByUserId = userId.Value,
        });

        // Keep the legacy Notes column in sync so PDF/audit readers still work.
        _agendaRepo.UpdateNotes(req.AgendaItemId, req.PlainText);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Hub join access: user can join if they can view the meeting via the
    /// standard access rules, OR they hold any of the chapter-scoped meeting
    /// permissions (View/Edit/CreateMeetings). The latter fallback matters for
    /// private non-draft meetings where the view rule only grants access to
    /// direct officers/delegates, but users with EditMeetings also need live
    /// updates on the meeting page. Anonymous users (<paramref name="userId"/>
    /// null) are allowed to join Public non-draft meetings in read-only mode.
    /// </summary>
    private bool CanJoin(Guid? userId, Data.Meetings.Meeting meeting) {
        // Anonymous viewers: only Public non-draft meetings are visible.
        if (userId == null) {
            return meeting.Status != MeetingStatus.Draft
                && meeting.Visibility == MeetingVisibility.Public;
        }
        if (MeetingAccessHelper.CanUserViewMeeting(
                userId, meeting, _roleRepo, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            return true;
        }
        foreach (var perm in new[] {
            PermissionIdentifier.ViewMeetings,
            PermissionIdentifier.EditMeetings,
            PermissionIdentifier.CreateMeetings
        }) {
            if (EndpointAuthorizationHelper.HasPermission(userId.Value, meeting.ChapterId, perm, _globalPermRepo, _chapterPermRepo, _chapterRepo))
                return true;
        }
        return false;
    }

    private bool HasEditPermission(Guid userId, Data.Meetings.Meeting meeting) {
        // Freeze edits on meetings that are no longer in progress. Even if
        // the user holds EditMeetings, a Completed/Archived meeting is
        // immutable — its protocol is considered final.
        if (meeting.Status != MeetingStatus.InProgress)
            return false;
        return EndpointAuthorizationHelper.HasPermission(
            userId, meeting.ChapterId, PermissionIdentifier.EditMeetings,
            _globalPermRepo, _chapterPermRepo, _chapterRepo);
    }

    private int ResolveSaveIntervalSeconds() {
        var raw = _optionRepo.ResolveValue("meetings.collab.save_interval_seconds", null, _chapterRepo);
        if (int.TryParse(raw, out var parsed) && parsed > 0)
            return parsed;
        return 10;
    }

    private static Dictionary<string, CollabAuthorInfo> ParseKnownAuthors(string json) {
        if (string.IsNullOrWhiteSpace(json))
            return new();
        try {
            return JsonSerializer.Deserialize<Dictionary<string, CollabAuthorInfo>>(json) ?? new();
        } catch {
            return new();
        }
    }

    private static string SerializeKnownAuthors(Dictionary<string, CollabAuthorInfo> map) {
        return JsonSerializer.Serialize(map);
    }
}
