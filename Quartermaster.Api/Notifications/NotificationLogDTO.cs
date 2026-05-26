using System;

namespace Quartermaster.Api.Notifications;

public class NotificationLogDTO {
    public Guid Id { get; set; }
    public string ChannelId { get; set; } = "";
    public string Recipient { get; set; } = "";
    public Guid? RecipientUserId { get; set; }
    public string Subject { get; set; } = "";
    public string? TriggerId { get; set; }
    public string? TemplateIdentifier { get; set; }
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public string Status { get; set; } = "";
    public string? Error { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
