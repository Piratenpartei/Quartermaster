using System;

namespace Quartermaster.Api.Meetings;

/// <summary>
/// Messages broadcast by <c>MeetingHub</c> to clients subscribed to a meeting's
/// group. Shared between server (sender) and Blazor client (receiver) so method
/// names stay in sync.
/// </summary>
public static class MeetingHubMethods {
    public const string AgendaItemChanged = "AgendaItemChanged";
    public const string MeetingStatusChanged = "MeetingStatusChanged";
    public const string PresenceChanged = "PresenceChanged";

    // Collaborative editing — server → client broadcasts forwarded from other
    // clients in the same meeting group.
    public const string ReceiveUpdate = "ReceiveUpdate";
    public const string ReceiveAwareness = "ReceiveAwareness";
}

/// <summary>
/// Initial document state returned from <c>MeetingHub.LoadDocument</c>.
/// The document state is Yjs-encoded as base64 so it can travel over the
/// JSON signalR protocol (which doesn't marshal byte arrays cleanly across
/// the JS client).
/// </summary>
public class CollabDocumentSnapshot {
    public string DocumentStateBase64 { get; set; } = "";
    public string PlainText { get; set; } = "";

    /// <summary>
    /// Persistent map of userId → {name, color} for every user that has ever
    /// contributed to this document. Accumulates across sessions so disconnected
    /// and deleted users' historical characters keep their original color and name.
    /// </summary>
    public System.Collections.Generic.Dictionary<string, CollabAuthorInfo> KnownAuthors { get; set; } = new();

    public int SaveIntervalSeconds { get; set; }
    public bool CanEdit { get; set; }

    /// <summary>Hex color (e.g., "#1e88e5") assigned to the caller for this document. Used for cursor + per-character highlighting.</summary>
    public string AssignedColor { get; set; } = "#1e88e5";
}

public class CollabSnapshotRequest {
    public System.Guid AgendaItemId { get; set; }
    public string DocumentStateBase64 { get; set; } = "";
    public string PlainText { get; set; } = "";
    public System.Collections.Generic.Dictionary<string, CollabAuthorInfo> KnownAuthors { get; set; } = new();
}

/// <summary>Display info for a past or present collaborative-editor author.</summary>
public class CollabAuthorInfo {
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#1e88e5";
}

public class AgendaItemChangedMessage {
    public Guid MeetingId { get; set; }
    public Guid AgendaItemId { get; set; }
    public string Reason { get; set; } = "";
}

public class MeetingStatusChangedMessage {
    public Guid MeetingId { get; set; }
    public MeetingStatus NewStatus { get; set; }
}

public class PresenceChangedMessage {
    public Guid MeetingId { get; set; }
    public Guid AgendaItemId { get; set; }
    public Guid UserId { get; set; }
    public bool IsPresent { get; set; }
}
