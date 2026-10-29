using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

public class ChapterMarksPreviewRequest {
    public Guid Id { get; set; }
    public bool NoMemberAdministration { get; set; }
    public bool UnableToAct { get; set; }
}

/// <summary>Where the waiting motions would go with the requested marks, for the confirmation dialog. Same access as <see cref="ChapterMarksEndpoint"/>; writes nothing.</summary>
public class ChapterMarksPreviewEndpoint : Endpoint<ChapterMarksPreviewRequest, List<ChapterMarksPreviewDTO>> {
    private readonly ChapterRepository _chapterRepo;
    private readonly MotionHandover _handover;
    private readonly PermissionContext _perms;

    public ChapterMarksPreviewEndpoint(ChapterRepository chapterRepo, MotionHandover handover, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _handover = handover;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/chapters/{Id}/marks/preview");
    }

    public override async Task HandleAsync(ChapterMarksPreviewRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var chapter = _chapterRepo.Get(req.Id);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (chapter.ParentChapterId == null) {
            AddError(r => r.Id, I18nKey.Error.Chapter.MarksRoot);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (!_perms.HasOnChapterOrAncestors(chapter.ParentChapterId.Value, PermissionIdentifier.ManageChapterCapacity)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var preview = _handover.Preview(chapter.Id, req.NoMemberAdministration, req.UnableToAct)
            .Select(p => new ChapterMarksPreviewDTO { TargetChapterName = p.TargetChapterName, Count = p.Count })
            .ToList();
        await SendAsync(preview, cancellation: ct);
    }
}
