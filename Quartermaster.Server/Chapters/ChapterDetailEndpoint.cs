using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.Chapters;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

public class ChapterDetailRequest {
    public Guid Id { get; set; }
}

public class ChapterDetailResponse {
    public ChapterDTO Chapter { get; set; } = new();
    public Guid? ParentChapterId { get; set; }
    public string? ParentChapterName { get; set; }
    public List<string> AdministrativeDivisionNames { get; set; } = [];
    public List<ChapterOfficerDTO> Officers { get; set; } = new();
    public List<ChapterDTO> Children { get; set; } = new();
    public bool CanManageMarks { get; set; }
}

public class ChapterDetailEndpoint : Endpoint<ChapterDetailRequest, ChapterDetailResponse> {
    private readonly ChapterRepository _chapterRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly DbContext _context;
    private readonly PermissionContext _perms;

    public ChapterDetailEndpoint(ChapterRepository chapterRepo, ChapterOfficerRepository officerRepo,
        AdministrativeDivisionRepository adminDivRepo, DbContext context, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _officerRepo = officerRepo;
        _adminDivRepo = adminDivRepo;
        _context = context;
        _perms = perms;
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

        var divisionIds = _chapterRepo.GetAdministrativeDivisions(chapter.Id);
        var divisionNames = divisionIds
            .Select(id => _adminDivRepo.Get(id))
            .Where(d => d != null && !string.IsNullOrEmpty(d.Name))
            .Select(d => string.IsNullOrEmpty(d!.AdminCode) ? d.Name : $"{d.Name} ({d.AdminCode})")
            .ToList();

        var officers = _officerRepo.GetForChapter(chapter.Id);
        var officerMemberIds = officers.Select(o => o.MemberId).ToList();
        var members = _context.Members.Where(m => officerMemberIds.Contains(m.Id)).ToList();

        var officerDtos = officers
            .Select(o => {
                var member = members.FirstOrDefault(m => m.Id == o.MemberId);
                return new ChapterOfficerDTO {
                    MemberId = o.MemberId,
                    MemberFirstName = member?.FirstName ?? "",
                    MemberLastName = member?.LastName ?? "",
                    ChapterId = o.ChapterId,
                    ChapterName = chapter.Name,
                    AssociateType = o.AssociateType
                };
            })
            .OrderBy(d => d.AssociateType.SortRank())
            .ThenBy(d => d.MemberLastName)
            .ThenBy(d => d.MemberFirstName)
            .ToList();

        var chapterCodes = _chapterRepo.GetExternalCodes(chapter.Id);
        var children = _chapterRepo.GetChildren(chapter.Id);
        var chapterDto = chapter.ToDto(chapterCodes, divisionIds);
        var canManageMarks = false;
        if (_perms.UserId != null) {
            chapterDto.ContactEmail = chapter.ContactEmail;
            chapterDto.NoMemberAdministration = chapter.NoMemberAdministration;
            chapterDto.UnableToAct = chapter.UnableToAct;
            canManageMarks = chapter.ParentChapterId != null && _perms.HasOnChapterOrAncestors(chapter.ParentChapterId.Value, PermissionIdentifier.ManageChapterCapacity);
        }

        await SendAsync(new ChapterDetailResponse {
            Chapter = chapterDto,
            ParentChapterId = chapter.ParentChapterId,
            ParentChapterName = parentName,
            AdministrativeDivisionNames = divisionNames,
            Officers = officerDtos,
            Children = _chapterRepo.ToDtos(children, includeMemberFields: _perms.UserId != null),
            CanManageMarks = canManageMarks
        }, cancellation: ct);
    }
}
