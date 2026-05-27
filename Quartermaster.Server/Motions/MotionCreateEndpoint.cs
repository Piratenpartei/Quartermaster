using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Quartermaster.Api.Motions;
using Quartermaster.Rendering;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Motions;

public class MotionCreateEndpoint : Endpoint<MotionCreateRequest, MotionDTO> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly NotificationDispatcher _notifications;

    public MotionCreateEndpoint(
        MotionRepository motionRepo,
        ChapterRepository chapterRepo,
        NotificationDispatcher notifications) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _notifications = notifications;
    }

    public override void Configure() {
        Post("/api/motions");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(Program.AnonymousCreateRateLimitPolicy));
    }

    public override async Task HandleAsync(MotionCreateRequest req, CancellationToken ct) {
        var chapter = _chapterRepo.Get(req.ChapterId);
        if (chapter == null) {
            AddError(r => r.ChapterId, "Die gewählte Gliederung existiert nicht.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var motion = new Motion {
            ChapterId = req.ChapterId,
            AuthorName = req.AuthorName,
            AuthorEmail = req.AuthorEmail,
            Title = req.Title,
            Text = MarkdownService.ToHtml(req.Text, SanitizationProfile.Strict),
            IsPublic = false,
            ApprovalStatus = MotionApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _motionRepo.Create(motion);

        var chapterName = chapter.Name;
        var payload = new MotionSubmittedPayload(
            motion.Id, motion.ChapterId, motion.Title, motion.AuthorName, chapterName);
        await _notifications.DispatchAsync(
            NotificationTriggers.MotionSubmitted,
            payload,
            _ => new Dictionary<string, object> {
                ["motion"] = new {
                    motion.Id,
                    motion.Title,
                    motion.AuthorName,
                    motion.CreatedAt
                },
                ["chapter"] = new { Id = motion.ChapterId, Name = chapterName }
            },
            sourceEntityType: "Motion",
            sourceEntityId: motion.Id,
            ct: ct);

        await SendAsync(new MotionDTO {
            Id = motion.Id,
            ChapterId = motion.ChapterId,
            AuthorName = motion.AuthorName,
            Title = motion.Title,
            IsPublic = false,
            ApprovalStatus = 0,
            CreatedAt = motion.CreatedAt
        }, cancellation: ct);
    }
}
