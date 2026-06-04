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

    /// <summary>Resolved member id when the channel address belongs to a Member (set by all bulk-to-member paths). May be set even when <see cref="RecipientUserId"/> is null because most members never log in.</summary>
    [Column]
    public Guid? RecipientMemberId { get; set; }

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

    /// <summary>RFC 5322 Message-Id of the outbound email; the join key for MDN replies arriving via IMAP.</summary>
    [Column]
    public string? MessageId { get; set; }

    [Column]
    public DateTime? MdnRequestedAt { get; set; }

    [Column]
    public DateTime? MdnReceivedAt { get; set; }

    /// <summary>RFC 3798 disposition type the recipient's client reported (e.g. <c>displayed</c>, <c>deleted</c>, <c>processed</c>).</summary>
    [Column]
    public string? MdnDisposition { get; set; }

    [Column]
    public DateTime? BouncedAt { get; set; }

    /// <summary>RFC 3464 delivery-status action + diagnostic-code for a bounced send (e.g. <c>failed: 550 5.1.1 user unknown</c>).</summary>
    [Column]
    public string? BounceDiagnostic { get; set; }
}
