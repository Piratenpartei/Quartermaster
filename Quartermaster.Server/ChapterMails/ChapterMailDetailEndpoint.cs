using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Templates;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ChapterMails;

public class ChapterMailIdRequest {
    public Guid Id { get; set; }
}

public class ChapterMailDetailEndpoint : Endpoint<ChapterMailIdRequest, ChapterMailDTO> {
    private readonly ChapterMailRepository _mailRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly TemplateRepository _templateRepo;
    private readonly UserRepository _userRepo;
    private readonly PermissionContext _perms;

    public ChapterMailDetailEndpoint(ChapterMailRepository mailRepo, ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo, TemplateRepository templateRepo,
        UserRepository userRepo, PermissionContext perms) {
        _mailRepo = mailRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _templateRepo = templateRepo;
        _userRepo = userRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/chapter-mail/{Id}");
    }

    public override async Task HandleAsync(ChapterMailIdRequest req, CancellationToken ct) {
        var mail = _mailRepo.Get(req.Id);
        if (mail == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(mail.ChapterId, PermissionIdentifier.ManageChapterMail)) {
            await SendForbiddenAsync(ct);
            return;
        }

        await SendAsync(BuildDto(mail), cancellation: ct);
    }

    private ChapterMailDTO BuildDto(ChapterMail mail) {
        var chapter = _chapterRepo.Get(mail.ChapterId);
        var template = mail.TemplateId.HasValue ? _templateRepo.Get(mail.TemplateId.Value) : null;
        var createdBy = _userRepo.Get(mail.CreatedByUserId);
        var sentBy = mail.SentByUserId.HasValue ? _userRepo.Get(mail.SentByUserId.Value) : null;

        var targetName = mail.TargetType == ChapterMailTargetType.Chapter
            ? _chapterRepo.Get(mail.TargetId)?.Name ?? ""
            : _adminDivRepo.Get(mail.TargetId)?.Name ?? "";

        return new ChapterMailDTO {
            Id = mail.Id,
            ChapterId = mail.ChapterId,
            ChapterName = chapter?.Name ?? "",
            Title = mail.Title,
            Subject = mail.Subject,
            TargetType = mail.TargetType,
            TargetId = mail.TargetId,
            TargetName = targetName,
            IncludeDescendants = mail.IncludeDescendants,
            TemplateId = mail.TemplateId,
            TemplateDisplayName = template?.DisplayName,
            Content = mail.Content,
            CreatedAt = new DateTimeOffset(mail.CreatedAt, TimeSpan.Zero),
            CreatedByUserId = mail.CreatedByUserId,
            CreatedByDisplayName = DisplayName(createdBy),
            SentAt = mail.SentAt.HasValue ? new DateTimeOffset(mail.SentAt.Value, TimeSpan.Zero) : null,
            SentByUserId = mail.SentByUserId,
            SentByDisplayName = DisplayName(sentBy),
            RecipientCount = mail.RecipientCount,
            DeletedAt = mail.DeletedAt.HasValue ? new DateTimeOffset(mail.DeletedAt.Value, TimeSpan.Zero) : null
        };
    }

    private static string? DisplayName(User? user) {
        if (user == null)
            return null;
        var full = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(full) ? user.Username : full;
    }
}
