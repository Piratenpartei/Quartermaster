using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Collab;

/// <summary>
/// Generic store for live-edited CRDT documents. Polymorphic on EntityType
/// so future live-edited entities can reuse the same table. Today only
/// "AgendaItem" is stored.
/// </summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class CollabDocument {
    public const string TableName = "CollabDocuments";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Polymorphic discriminator (e.g., "AgendaItem").</summary>
    public string EntityType { get; set; } = "";

    /// <summary>FK target, no DB-level constraint (soft ref).</summary>
    public Guid EntityId { get; set; }

    /// <summary>Full Yjs binary state snapshot — base64-encoded.</summary>
    public string DocumentState { get; set; } = "";

    /// <summary>Denormalized plain text extracted from the Yjs doc at save time. Read by PDF renderer and audit log.</summary>
    public string PlainText { get; set; } = "";

    /// <summary>JSON dictionary mapping Yjs client IDs (uint) to Quartermaster user IDs (Guid). Append-only.</summary>
    public string ClientUserMap { get; set; } = "{}";

    public DateTime LastUpdatedAt { get; set; }
    public Guid? LastUpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
