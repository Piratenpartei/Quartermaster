using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Members;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberDetailRequest {
    public Guid Id { get; set; }
}

public class MemberDetailEndpoint : Endpoint<MemberDetailRequest, MemberDetailDTO> {
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public MemberDetailEndpoint(
        MemberRepository memberRepo,
        ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo,
        UserChapterPermissionRepository chapterPermRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/members/{Id}");
    }

    public override async Task HandleAsync(MemberDetailRequest req, CancellationToken ct) {
        var member = _memberRepo.Get(req.Id);
        if (member == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewAllMembers, _globalPermRepo)) {
            if (member.ChapterId.HasValue) {
                if (!_chapterPermRepo.HasPermissionWithInheritance(userId.Value, member.ChapterId.Value, PermissionIdentifier.ViewMembers, _chapterRepo)) {
                    await SendForbiddenAsync(ct);
                    return;
                }
            } else {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        var chapterName = "";
        if (member.ChapterId.HasValue) {
            var chapter = _chapterRepo.Get(member.ChapterId.Value);
            if (chapter != null)
                chapterName = chapter.Name;
        }

        var adminDivName = "";
        var isAdminDivOrphaned = false;
        if (member.ResidenceAdministrativeDivisionId.HasValue) {
            var div = _adminDivRepo.Get(member.ResidenceAdministrativeDivisionId.Value);
            if (div != null) {
                adminDivName = div.Name;
                isAdminDivOrphaned = div.IsOrphaned;
            }
        }

        await SendAsync(new MemberDetailDTO {
            Id = member.Id,
            MemberNumber = member.MemberNumber,
            AdmissionReference = member.AdmissionReference,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Street = member.Street,
            Country = member.Country,
            PostCode = member.PostCode,
            City = member.City,
            Phone = member.Phone,
            EMail = member.EMail,
            DateOfBirth = member.DateOfBirth,
            Citizenship = member.Citizenship,
            MembershipFee = member.MembershipFee,
            ReducedFee = member.ReducedFee,
            FirstFee = member.FirstFee,
            OpenFeeTotal = member.OpenFeeTotal,
            ReducedFeeEnd = member.ReducedFeeEnd,
            EntryDate = member.EntryDate,
            ExitDate = member.ExitDate,
            FederalState = member.FederalState,
            County = member.County,
            Municipality = member.Municipality,
            IsPending = member.IsPending,
            HasVotingRights = member.HasVotingRights,
            ReceivesSurveys = member.ReceivesSurveys,
            ReceivesActions = member.ReceivesActions,
            ReceivesNewsletter = member.ReceivesNewsletter,
            PostBounce = member.PostBounce,
            ChapterId = member.ChapterId,
            ChapterName = chapterName,
            ResidenceAdministrativeDivisionId = member.ResidenceAdministrativeDivisionId,
            ResidenceAdministrativeDivisionName = adminDivName,
            IsAdminDivisionOrphaned = isAdminDivOrphaned,
            UserId = member.UserId,
            LastImportedAt = member.LastImportedAt
        }, cancellation: ct);
    }
}
