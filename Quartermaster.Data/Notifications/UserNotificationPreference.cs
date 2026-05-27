using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Notifications;

/// <summary>
/// Explicit per-user override for one (trigger, channel) pair. Absence of a row means
/// "use the channel's default" (smtp on, telegram/pdf off in Phase 3).
/// </summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class UserNotificationPreference {
    public const string TableName = "UserNotificationPreferences";

    [PrimaryKey(Order = 0)]
    public Guid UserId { get; set; }

    [PrimaryKey(Order = 1)]
    public string TriggerId { get; set; } = "";

    [PrimaryKey(Order = 2)]
    public string ChannelId { get; set; } = "";

    public bool Enabled { get; set; }
}
