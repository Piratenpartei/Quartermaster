using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Events;
using Quartermaster.Api.Members;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Data.Motions;
using Quartermaster.Rendering;
using Quartermaster.Server.Email;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Events;

public class ChecklistItemExecutor {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly EmailService _emailService;
    private readonly NotificationTemplateGlobals _globals;

    public ChecklistItemExecutor(MotionRepository motionRepo, ChapterRepository chapterRepo,
        EmailService emailService, NotificationTemplateGlobals globals) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _emailService = emailService;
        _globals = globals;
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

        var textHtml = MarkdownService.ToHtml(config.MotionText, SanitizationProfile.Strict);
        var motion = Motion.Create(
            config.ChapterId.Value,
            authorName: "System (Event)",
            authorEmail: "",
            title: config.MotionTitle ?? "",
            textMarkdown: config.MotionText,
            textHtml: textHtml,
            nowUtc: DateTime.UtcNow);

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

        var globalsBlock = _globals.Build();
        var chapter = parentEvent != null ? _chapterRepo.Get(parentEvent.ChapterId) : null;
        var chapterBlock = chapter?.ToDto();
        var eventBlock = parentEvent?.ToDetailDto(chapter?.Name ?? "");

        Dictionary<string, object> ModelFor(Member? member) {
            var model = new Dictionary<string, object> {
                ["globals"] = globalsBlock
            };
            if (chapterBlock != null)
                model["chapter"] = chapterBlock;
            if (eventBlock != null)
                model["event"] = eventBlock;
            if (member != null)
                model["member"] = member.ToDetailDto(chapter?.Name ?? "");
            return model;
        }

        var (_, error) = await _emailService.SendEmailAsync(
            config.TargetType, config.TargetId ?? Guid.Empty, config.TemplateId,
            descriptionOverride, config.ManualAddresses,
            "EventChecklistItem", item.Id,
            resolutionChapterId: parentEvent?.ChapterId,
            modelFactory: ModelFor);
        if (error != null)
            return (null, error);

        return (null, null);
    }
}
