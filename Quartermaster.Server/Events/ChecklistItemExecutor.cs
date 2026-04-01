using System;
using System.Text.Json;
using Quartermaster.Api.Rendering;
using Quartermaster.Data.Events;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Email;

namespace Quartermaster.Server.Events;

public class ChecklistItemExecutor {
    private readonly MotionRepository _motionRepo;
    private readonly EmailService _emailService;

    public ChecklistItemExecutor(MotionRepository motionRepo, EmailService emailService) {
        _motionRepo = motionRepo;
        _emailService = emailService;
    }

    public (Guid? ResultId, string? Error) Execute(EventChecklistItem item, Event? parentEvent = null) {
        return item.ItemType switch {
            ChecklistItemType.CreateMotion => ExecuteCreateMotion(item),
            ChecklistItemType.SendEmail => ExecuteSendEmail(item, parentEvent),
            _ => (null, null)
        };
    }

    private (Guid? ResultId, string? Error) ExecuteCreateMotion(EventChecklistItem item) {
        if (string.IsNullOrEmpty(item.Configuration))
            return (null, "No configuration for motion creation");

        var config = JsonSerializer.Deserialize<MotionConfig>(item.Configuration);
        if (config == null)
            return (null, "Invalid motion configuration");

        var motion = new Motion {
            ChapterId = config.ChapterId,
            AuthorName = "System (Event)",
            AuthorEMail = "",
            Title = config.MotionTitle,
            Text = MarkdownService.ToHtml(config.MotionText, SanitizationProfile.Strict),
            IsPublic = false,
            ApprovalStatus = MotionApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _motionRepo.Create(motion);
        return (motion.Id, null);
    }

    private (Guid? ResultId, string? Error) ExecuteSendEmail(EventChecklistItem item, Event? parentEvent) {
        if (string.IsNullOrEmpty(item.Configuration))
            return (null, "No configuration for email sending");

        var config = JsonSerializer.Deserialize<EmailConfig>(item.Configuration);
        if (config == null)
            return (null, "Invalid email configuration");

        // If useDescription is set, use the event description as template content
        string? descriptionOverride = null;
        if (config.UseDescription && parentEvent != null) {
            var desc = parentEvent.Description ?? "";
            var dateStr = parentEvent.EventDate?.ToString("dd.MM.yyyy") ?? "";
            desc = desc.Replace("{{date}}", dateStr).Replace("{{datum}}", dateStr);
            descriptionOverride = desc;
        }

        var (count, error) = _emailService.SendEmail(
            config.TargetType, config.TargetId, config.TemplateIdentifier,
            descriptionOverride, config.ManualAddresses,
            "EventChecklistItem", item.Id);
        if (error != null)
            return (null, error);

        return (null, null);
    }

    private class MotionConfig {
        public Guid ChapterId { get; set; }
        public string MotionTitle { get; set; } = "";
        public string MotionText { get; set; } = "";
    }

    private class EmailConfig {
        public string TargetType { get; set; } = "";
        public Guid TargetId { get; set; }
        public string TemplateIdentifier { get; set; } = "";
        public bool UseDescription { get; set; }
        public string? ManualAddresses { get; set; }
    }
}
