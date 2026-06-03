using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Email;

namespace Quartermaster.Server.Events;

public class ChecklistResolveRecipientsEndpoint : Endpoint<RecipientPreviewRequest, RecipientPreviewResponse> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly EmailService _emailService;
    private readonly PermissionContext _perms;

    public ChecklistResolveRecipientsEndpoint(
        EventRepository eventRepo,
        ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo,
        EmailService emailService,
        PermissionContext perms) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _emailService = emailService;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/resolve-recipients");
    }

    public override async Task HandleAsync(RecipientPreviewRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.EventId);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(ev.ChapterId, PermissionIdentifier.EditEvents)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var config = req.Configuration;
        var targetType = config.TargetType ?? "";
        var recipients = new List<RecipientPreviewItem>();

        if (targetType == "ManualAddresses") {
            var addresses = (config.ManualAddresses ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => e.Contains('@'))
                .Distinct();
            foreach (var addr in addresses) {
                recipients.Add(new RecipientPreviewItem {
                    Email = addr,
                    Reason = "Manuelle Adresse"
                });
            }
        } else if (config.TargetId.HasValue && config.TargetId.Value != Guid.Empty) {
            var targetName = ResolveTargetName(targetType, config.TargetId.Value);
            var members = _emailService.FetchTargetMembers(targetType, config.TargetId.Value);
            foreach (var member in members.Where(m => !string.IsNullOrEmpty(m.Email))) {
                recipients.Add(new RecipientPreviewItem {
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email!,
                    Reason = BuildReason(targetType, targetName)
                });
            }
        }

        await SendAsync(new RecipientPreviewResponse { Recipients = recipients }, cancellation: ct);
    }

    private string? ResolveTargetName(string targetType, Guid targetId) => targetType switch {
        "Chapter" => _chapterRepo.Get(targetId)?.Name,
        "AdministrativeDivision" => _adminDivRepo.Get(targetId)?.Name,
        _ => null
    };

    private static string BuildReason(string targetType, string? targetName) {
        var label = string.IsNullOrEmpty(targetName) ? "?" : targetName;
        return targetType switch {
            "Chapter" => $"Mitglied der Gliederung {label}",
            "AdministrativeDivision" => $"Mitglied im Gebiet {label}",
            _ => label
        };
    }
}
