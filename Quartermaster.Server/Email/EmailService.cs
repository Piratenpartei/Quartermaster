using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Rendering;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Messaging;

namespace Quartermaster.Server.Email;

public class EmailService {
    private readonly TemplateRepository _templateRepo;
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly EmailMessageChannel _emailChannel;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        TemplateRepository templateRepo,
        MemberRepository memberRepo,
        ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo,
        EmailMessageChannel emailChannel,
        ILogger<EmailService> logger) {
        _templateRepo = templateRepo;
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _emailChannel = emailChannel;
        _logger = logger;
    }

    public async Task<(int Count, string? Error)> SendEmailAsync(
        string targetType, Guid targetId, Guid? templateId,
        string? descriptionOverride, string? manualAddresses,
        string? sourceEntityType = null, Guid? sourceEntityId = null,
        Guid? resolutionChapterId = null) {

        var effectiveResolutionChapter = resolutionChapterId
            ?? (targetType == "Chapter" && targetId != Guid.Empty ? targetId : (Guid?)null);
        Template? template = templateId.HasValue
            ? _templateRepo.ResolveById(templateId.Value, effectiveResolutionChapter, _chapterRepo)
            : null;

        string? bodyContent = descriptionOverride ?? template?.Body;
        if (string.IsNullOrEmpty(bodyContent))
            return (0, "Kein Template-Inhalt verfügbar.");

        var subject = template?.Subject ?? template?.DisplayName ?? "Nachricht";
        var count = 0;

        if (targetType == "ManualAddresses" && !string.IsNullOrEmpty(manualAddresses)) {
            var addresses = manualAddresses
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => e.Contains('@'))
                .Distinct()
                .ToList();

            foreach (var addr in addresses) {
                await EnqueueEmailAsync(addr, subject, bodyContent, templateId,
                    null, sourceEntityType, sourceEntityId);
                count++;
            }
        } else {
            var members = FetchTargetMembers(targetType, targetId);
            foreach (var member in members) {
                if (string.IsNullOrEmpty(member.Email))
                    continue;
                await EnqueueEmailAsync(member.Email, subject, bodyContent, templateId,
                    member, sourceEntityType, sourceEntityId);
                count++;
            }
        }

        _logger.LogInformation("Enqueued {Count} emails for {TargetType}/{TargetId}",
            count, targetType, targetId);
        return (count, null);
    }

    private async Task EnqueueEmailAsync(string recipient, string subject, string templateContent,
        Guid? templateId, Member? member,
        string? sourceEntityType, Guid? sourceEntityId) {

        var model = new Dictionary<string, object>();
        if (member != null) {
            model["member"] = new {
                member.FirstName,
                member.LastName,
                member.Email,
                member.MemberNumber,
                member.City,
                member.PostCode
            };
        }

        var (html, error) = await TemplateRenderer.RenderHtmlAsync(templateContent, model);
        if (error != null)
            _logger.LogWarning("Template render error for {Recipient}: {Error}", recipient, error);
        var htmlBody = html ?? templateContent;

        var metadata = templateId.HasValue
            ? new Dictionary<string, string> { [NotificationLogMetadataKeys.TemplateIdentifier] = templateId.Value.ToString() }
            : null;
        await _emailChannel.SendAsync(new ChannelMessage(
            ChannelAddress: recipient,
            Subject: subject,
            Body: htmlBody,
            SourceEntityType: sourceEntityType,
            SourceEntityId: sourceEntityId,
            Metadata: metadata));
    }

    private List<Member> FetchTargetMembers(string targetType, Guid targetId) {
        if (targetType == "Chapter" && targetId != Guid.Empty) {
            var chapterIds = _chapterRepo.GetDescendantIds(targetId);
            return _memberRepo.GetByChapterIds(chapterIds);
        }

        if (targetType == "AdministrativeDivision" && targetId != Guid.Empty) {
            var divisionIds = _adminDivRepo.GetDescendantIds(targetId);
            return _memberRepo.GetByAdministrativeDivisionIds(divisionIds);
        }

        return new List<Member>();
    }
}
