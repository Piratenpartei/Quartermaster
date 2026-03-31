using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.Chapters;
using Quartermaster.Data;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Chapters;

public class ChapterDetailRequest {
    public Guid Id { get; set; }
}

public class ChapterDetailResponse {
    public ChapterDTO Chapter { get; set; } = new();
    public Guid? ParentChapterId { get; set; }
    public string? ParentChapterName { get; set; }
    public List<ChapterOfficerDTO> Officers { get; set; } = new();
    public List<ChapterDTO> Children { get; set; } = new();
}

public class ChapterDetailEndpoint : Endpoint<ChapterDetailRequest, ChapterDetailResponse> {
    private readonly ChapterRepository _chapterRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly DbContext _context;

    public ChapterDetailEndpoint(ChapterRepository chapterRepo, ChapterOfficerRepository officerRepo, DbContext context) {
        _chapterRepo = chapterRepo;
        _officerRepo = officerRepo;
        _context = context;
    }

    public override void Configure() {
        Get("/api/chapters/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterDetailRequest req, CancellationToken ct) {
        var chapter = _chapterRepo.Get(req.Id);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        string? parentName = null;
        if (chapter.ParentChapterId.HasValue) {
            var parent = _chapterRepo.Get(chapter.ParentChapterId.Value);
            if (parent != null)
                parentName = parent.Name;
        }

        var officers = _officerRepo.GetForChapter(chapter.Id);
        var officerMemberIds = officers.Select(o => o.MemberId).ToList();
        var members = _context.Members.Where(m => officerMemberIds.Contains(m.Id)).ToList();

        var officerDtos = officers.Select(o => {
            var member = members.FirstOrDefault(m => m.Id == o.MemberId);
            return new ChapterOfficerDTO {
                MemberId = o.MemberId,
                MemberNumber = member?.MemberNumber ?? 0,
                MemberFirstName = member?.FirstName ?? "",
                MemberLastName = member?.LastName ?? "",
                ChapterId = o.ChapterId,
                ChapterName = chapter.Name,
                AssociateType = (int)o.AssociateType
            };
        }).ToList();

        var children = _chapterRepo.GetChildren(chapter.Id);

        await SendAsync(new ChapterDetailResponse {
            Chapter = chapter.ToDto(),
            ParentChapterId = chapter.ParentChapterId,
            ParentChapterName = parentName,
            Officers = officerDtos,
            Children = children.Select(c => c.ToDto()).ToList()
        }, cancellation: ct);
    }
}
