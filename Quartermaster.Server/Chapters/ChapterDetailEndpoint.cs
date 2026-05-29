using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.Chapters;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
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
    public string? AdministrativeDivisionName { get; set; }
    public List<ChapterOfficerDTO> Officers { get; set; } = new();
    public List<ChapterDTO> Children { get; set; } = new();
}

public class ChapterDetailEndpoint : Endpoint<ChapterDetailRequest, ChapterDetailResponse> {
    private readonly ChapterRepository _chapterRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly DbContext _context;

    public ChapterDetailEndpoint(ChapterRepository chapterRepo, ChapterOfficerRepository officerRepo,
        AdministrativeDivisionRepository adminDivRepo, DbContext context) {
        _chapterRepo = chapterRepo;
        _officerRepo = officerRepo;
        _adminDivRepo = adminDivRepo;
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

        string? divisionName = null;
        if (chapter.AdministrativeDivisionId.HasValue) {
            var division = _adminDivRepo.Get(chapter.AdministrativeDivisionId.Value);
            if (division != null)
                divisionName = division.Name;
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
                AssociateType = o.AssociateType
            };
        }).ToList();

        var children = _chapterRepo.GetChildren(chapter.Id);

        await SendAsync(new ChapterDetailResponse {
            Chapter = new ChapterDTO {
                Id = chapter.Id,
                Name = chapter.Name,
                ShortCode = chapter.ShortCode,
                AdministrativeDivisionId = chapter.AdministrativeDivisionId,
                ExternalCode = chapter.ExternalCode,
                ParentChapterId = chapter.ParentChapterId
            },
            ParentChapterId = chapter.ParentChapterId,
            ParentChapterName = parentName,
            AdministrativeDivisionName = divisionName,
            Officers = officerDtos,
            Children = children.Select(c => new ChapterDTO {
                Id = c.Id,
                Name = c.Name,
                ShortCode = c.ShortCode,
                AdministrativeDivisionId = c.AdministrativeDivisionId,
                ExternalCode = c.ExternalCode,
                ParentChapterId = c.ParentChapterId
            }).ToList()
        }, cancellation: ct);
    }
}
