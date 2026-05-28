using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Notifications;

[Table(TableName)]
public class NotificationLog {
    public const string TableName = "NotificationLogs";

    [Column, PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable channel id — <c>"email"</c> / <c>"telegram"</c> / <c>"pdf"</c>.</summary>
    [Column]
    public string ChannelId { get; set; } = "";

    /// <summary>Channel-specific address: email, Telegram chat id, multi-line postal address.</summary>
    [Column]
    public string Recipient { get; set; } = "";

    /// <summary>Resolved user id when the notification came from a trigger; null for ad-hoc sends.</summary>
    [Column]
    public Guid? RecipientUserId { get; set; }

    [Column]
    public string Subject { get; set; } = "";

    /// <summary>Trigger identifier (<c>"motion_submitted"</c> etc.); null for ad-hoc sends.</summary>
    [Column]
    public string? TriggerId { get; set; }

    /// <summary>Template option key (<c>"templates.membershipapplication.approved.email.body"</c>).</summary>
    [Column]
    public string? TemplateIdentifier { get; set; }

    [Column]
    public string? SourceEntityType { get; set; }

    [Column]
    public Guid? SourceEntityId { get; set; }

    [Column]
    public string Status { get; set; } = "Pending";

    [Column]
    public string? Error { get; set; }

    [Column]
    public int AttemptCount { get; set; }

    [Column]
    public DateTime CreatedAt { get; set; }

    [Column]
    public DateTime? SentAt { get; set; }

    /// <summary>Rendered body the channel ships (HTML for email, markdown for Telegram, plain text for PDF).</summary>
    [Column]
    public string? Body { get; set; }
}
