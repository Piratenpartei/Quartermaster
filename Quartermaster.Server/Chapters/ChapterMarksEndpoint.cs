using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

/// <summary>
/// Sets or lifts the capacity marks of a chapter and hands its waiting motions over, in one transaction. Requires
/// <see cref="PermissionIdentifier.ManageChapterCapacity"/> on the parent chapter or any chapter above it, so never the chapter's own officers.
/// A concurrent vote can make the handover fail with "Record has changed since last read"; the transaction rolls back, the request
/// fails with 500 and the user retries. A submission whose routing read the chapter before the mark was committed may still land
/// at the marked chapter; saving the marks again hands it over.
/// </summary>
public class ChapterMarksEndpoint : Endpoint<ChapterMarksRequest, ChapterMarksResponse> {
    private readonly ChapterRepository _chapterRepo;
    private readonly MotionHandover _handover;
    private readonly DbContext _context;
    private readonly PermissionContext _perms;

    public ChapterMarksEndpoint(ChapterRepository chapterRepo, MotionHandover handover, DbContext context, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _handover = handover;
        _context = context;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/chapters/{Id}/marks");
    }

    public override async Task HandleAsync(ChapterMarksRequest req, CancellationToken ct) {
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

        int handedOver;
        using (var tx = _context.BeginTransaction()) {
            _chapterRepo.SetMarks(chapter.Id, req.NoMemberAdministration, req.UnableToAct);
            handedOver = _handover.HandOver(chapter.Id);
            tx.Commit();
        }
        _handover.SendNotifications();

        await SendAsync(new ChapterMarksResponse { HandedOver = handedOver }, cancellation: ct);
    }
}
