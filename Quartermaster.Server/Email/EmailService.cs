using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Rendering;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Email;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Email;

public class EmailService {
    private readonly EmailLogRepository _emailLogRepo;
    private readonly OptionRepository _optionRepo;
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly Channel<EmailMessage> _emailChannel;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        EmailLogRepository emailLogRepo,
        OptionRepository optionRepo,
        MemberRepository memberRepo,
        ChapterRepository chapterRepo,
        Channel<EmailMessage> emailChannel,
        ILogger<EmailService> logger) {
        _emailLogRepo = emailLogRepo;
        _optionRepo = optionRepo;
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
        _emailChannel = emailChannel;
        _logger = logger;
    }

    public (int Count, string? Error) SendEmail(
        string targetType, Guid targetId, string templateIdentifier,
        string? descriptionOverride, string? manualAddresses,
        string? sourceEntityType = null, Guid? sourceEntityId = null) {

        // Resolve template content
        string? templateContent = descriptionOverride;
        if (string.IsNullOrEmpty(templateContent) && !string.IsNullOrEmpty(templateIdentifier)) {
            templateContent = _optionRepo.ResolveValue(templateIdentifier, null, _chapterRepo);
        }

        if (string.IsNullOrEmpty(templateContent))
            return (0, "Kein Template-Inhalt verfügbar.");

        // Resolve subject from template identifier or default
        var subject = !string.IsNullOrEmpty(templateIdentifier)
            ? _optionRepo.GetDefinition(templateIdentifier)?.FriendlyName ?? "Nachricht"
            : "Nachricht";

        var count = 0;

        if (targetType == "ManualAddresses" && !string.IsNullOrEmpty(manualAddresses)) {
            var addresses = manualAddresses
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => e.Contains('@'))
                .Distinct()
                .ToList();

            foreach (var addr in addresses) {
                EnqueueEmail(addr, subject, templateContent, templateIdentifier,
                    null, sourceEntityType, sourceEntityId);
                count++;
            }
        } else {
            var members = FetchTargetMembers(targetType, targetId);
            foreach (var member in members) {
                if (string.IsNullOrEmpty(member.EMail))
                    continue;
                EnqueueEmail(member.EMail, subject, templateContent, templateIdentifier,
                    member, sourceEntityType, sourceEntityId);
                count++;
            }
        }

        _logger.LogInformation("Enqueued {Count} emails for {TargetType}/{TargetId}",
            count, targetType, targetId);
        return (count, null);
    }

    private void EnqueueEmail(string recipient, string subject, string templateContent,
        string? templateIdentifier, Member? member,
        string? sourceEntityType, Guid? sourceEntityId) {

        // Render personalized template
        var model = new Dictionary<string, object>();
        if (member != null) {
            model["member"] = new {
                member.FirstName,
                member.LastName,
                member.EMail,
                member.MemberNumber,
                member.City,
                member.PostCode
            };
        }

        var renderTask = TemplateRenderer.RenderAsync(templateContent, model);
        renderTask.Wait();
        var (html, error) = renderTask.Result;
        if (error != null)
            _logger.LogWarning("Template render error for {Recipient}: {Error}", recipient, error);
        var htmlBody = html ?? templateContent;

        // Create log entry
        var log = new EmailLog {
            Recipient = recipient,
            Subject = subject,
            TemplateIdentifier = templateIdentifier,
            SourceEntityType = sourceEntityType,
            SourceEntityId = sourceEntityId,
            Status = "Pending",
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        _emailLogRepo.Create(log);

        // Enqueue for background sending
        _emailChannel.Writer.TryWrite(new EmailMessage(log.Id, recipient, subject, htmlBody));
    }

    private List<Member> FetchTargetMembers(string targetType, Guid targetId) {
        if (targetType == "Chapter" && targetId != Guid.Empty) {
            var chapterIds = _chapterRepo.GetDescendantIds(targetId);
            return _memberRepo.GetByChapterIds(chapterIds);
        }

        if (targetType == "AdministrativeDivision" && targetId != Guid.Empty)
            return _memberRepo.GetByAdministrativeDivisionId(targetId);

        return new List<Member>();
    }
}
