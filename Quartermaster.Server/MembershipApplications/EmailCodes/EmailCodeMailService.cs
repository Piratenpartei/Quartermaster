using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.MembershipApplications.EmailCodes;

public class EmailCodeMailService {
    public const string TemplateIdentifier = "templates.submission.membershipapplication.code.email";
    public const string TriggerId = "membership_application_email_code";

    private readonly TemplateRepository _templateRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly EmailMessageChannel _emailChannel;
    private readonly NotificationTemplateGlobals _globals;
    private readonly ILogger<EmailCodeMailService> _logger;

    public EmailCodeMailService(
        TemplateRepository templateRepo,
        ChapterRepository chapterRepo,
        EmailMessageChannel emailChannel,
        NotificationTemplateGlobals globals,
        ILogger<EmailCodeMailService> logger) {
        _templateRepo = templateRepo;
        _chapterRepo = chapterRepo;
        _emailChannel = emailChannel;
        _globals = globals;
        _logger = logger;
    }

    public async Task SendAsync(string email, Guid? chapterId, string code, int minutes, CancellationToken ct) {
        var template = _templateRepo.Resolve(TemplateIdentifier, chapterId, _chapterRepo);
        if (template == null || string.IsNullOrEmpty(template.Body)) {
            _logger.LogWarning("Email code template missing ({Identifier})", TemplateIdentifier);
            return;
        }

        var chapter = chapterId.HasValue ? _chapterRepo.Get(chapterId.Value) : null;
        var model = new Dictionary<string, object> {
            ["globals"] = _globals.Build(),
            ["code"] = code,
            ["minutes"] = minutes,
            ["chapter"] = _chapterRepo.WithTemplateContact(chapter?.ToDto() ?? new ChapterDTO { Id = chapterId ?? Guid.Empty })
        };

        var (subject, _) = await TemplateRenderer.RenderTextAsync(template.Subject ?? "", model);
        var (body, _) = await TemplateRenderer.RenderHtmlAsync(template.Body, model);

        var metadata = new Dictionary<string, string> {
            [NotificationLogMetadataKeys.TriggerId] = TriggerId,
            [NotificationLogMetadataKeys.TemplateIdentifier] = TemplateIdentifier
        };
        await _emailChannel.SendAsync(new ChannelMessage(
            ChannelAddress: email,
            Subject: subject ?? template.Subject ?? "",
            Body: body ?? template.Body,
            Metadata: metadata), ct);
    }
}
