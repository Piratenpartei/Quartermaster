using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Events;

public class MemberEmailService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberEmailService> _logger;

    public MemberEmailService(IServiceScopeFactory scopeFactory, ILogger<MemberEmailService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public (int RecipientCount, string? Error) SendEmail(
        string targetType, Guid targetId, string templateIdentifier,
        string? descriptionOverride = null, string? manualAddresses = null) {

        using var scope = _scopeFactory.CreateScope();
        var chapterRepo = scope.ServiceProvider.GetRequiredService<ChapterRepository>();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();

        // Resolve recipients
        var emailAddresses = new List<string>();

        if (targetType == "ManualAddresses" && !string.IsNullOrWhiteSpace(manualAddresses)) {
            emailAddresses = manualAddresses
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => e.Contains('@'))
                .Distinct()
                .ToList();
        } else {
            var memberRepo = scope.ServiceProvider.GetRequiredService<MemberRepository>();

            List<Member> recipients;
            if (targetType == "Chapter" && targetId != Guid.Empty) {
                var chapterIds = chapterRepo.GetDescendantIds(targetId);
                var (members, _) = memberRepo.Search(null, null, 1, 100000);
                recipients = members.Where(m => m.ChapterId.HasValue && chapterIds.Contains(m.ChapterId.Value)).ToList();
            } else if (targetType == "AdministrativeDivision" && targetId != Guid.Empty) {
                var (members, _) = memberRepo.Search(null, null, 1, 100000);
                recipients = members.Where(m => m.ResidenceAdministrativeDivisionId == targetId).ToList();
            } else {
                return (0, "No target configured");
            }

            emailAddresses = recipients
                .Where(m => !string.IsNullOrEmpty(m.EMail))
                .Select(m => m.EMail!)
                .ToList();
        }

        if (emailAddresses.Count == 0)
            return (0, "No recipients found");

        // Resolve template
        string templateValue;
        if (!string.IsNullOrEmpty(descriptionOverride)) {
            templateValue = descriptionOverride;
        } else {
            var resolved = optionRepo.ResolveValue(templateIdentifier, null, chapterRepo);
            if (string.IsNullOrEmpty(resolved))
                return (0, $"Template '{templateIdentifier}' not found or empty");
            templateValue = resolved;
        }

        // STUB: Log what would be sent instead of actually sending
        _logger.LogInformation(
            "EMAIL STUB: Would send email to {Count} recipients using template '{Template}'",
            emailAddresses.Count, string.IsNullOrEmpty(descriptionOverride) ? templateIdentifier : "(event description)");

        foreach (var email in emailAddresses.Take(5)) {
            _logger.LogInformation("  -> {Email}", email);
        }

        if (emailAddresses.Count > 5)
            _logger.LogInformation("  -> ... and {Count} more", emailAddresses.Count - 5);

        return (emailAddresses.Count, null);
    }
}
