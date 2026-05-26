using System;
using System.Threading.Tasks;
using Quartermaster.Api.Events;
using Quartermaster.Api.Motions;
using Quartermaster.Rendering;
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

    public Task<(Guid? ResultId, string? Error)> ExecuteAsync(EventChecklistItem item, Event? parentEvent = null) {
        return item.ItemType switch {
            ChecklistItemType.CreateMotion => Task.FromResult(ExecuteCreateMotion(item)),
            ChecklistItemType.SendEmail => ExecuteSendEmailAsync(item, parentEvent),
            _ => Task.FromResult<(Guid?, string?)>((null, null))
        };
    }

    private (Guid? ResultId, string? Error) ExecuteCreateMotion(EventChecklistItem item) {
        var config = EventConfigSerializer.ParseConfig(item.Configuration);
        if (config == null || config.ChapterId == null || string.IsNullOrEmpty(config.MotionText))
            return (null, "Invalid motion configuration");

        var motion = new Motion {
            ChapterId = config.ChapterId.Value,
            AuthorName = "System (Event)",
            AuthorEmail = "",
            Title = config.MotionTitle ?? "",
            Text = MarkdownService.ToHtml(config.MotionText, SanitizationProfile.Strict),
            IsPublic = false,
            ApprovalStatus = MotionApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _motionRepo.Create(motion);
        return (motion.Id, null);
    }

    private async Task<(Guid? ResultId, string? Error)> ExecuteSendEmailAsync(EventChecklistItem item, Event? parentEvent) {
        var config = EventConfigSerializer.ParseConfig(item.Configuration);
        if (config == null || string.IsNullOrEmpty(config.TargetType))
            return (null, "Invalid email configuration");

        string? descriptionOverride = null;
        if (config.UseDescription && parentEvent != null) {
            var desc = parentEvent.Description ?? "";
            var dateStr = parentEvent.EventDate?.ToString("dd.MM.yyyy") ?? "";
            desc = desc.Replace("{{date}}", dateStr).Replace("{{datum}}", dateStr);
            descriptionOverride = desc;
        }

        var (_, error) = await _emailService.SendEmailAsync(
            config.TargetType, config.TargetId ?? Guid.Empty, config.TemplateIdentifier ?? "",
            descriptionOverride, config.ManualAddresses,
            "EventChecklistItem", item.Id);
        if (error != null)
            return (null, error);

        return (null, null);
    }
}
