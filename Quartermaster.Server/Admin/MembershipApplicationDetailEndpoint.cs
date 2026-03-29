using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationDetailRequest {
    public Guid Id { get; set; }
}

public class MembershipApplicationDetailEndpoint
    : Endpoint<MembershipApplicationDetailRequest, MembershipApplicationDetailDTO> {

    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MotionRepository _motionRepo;

    public MembershipApplicationDetailEndpoint(
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo,
        DueSelectionRepository dueSelectionRepo,
        MotionRepository motionRepo) {
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
        _dueSelectionRepo = dueSelectionRepo;
        _motionRepo = motionRepo;
    }

    public override void Configure() {
        Get("/api/admin/membershipapplications/{Id}");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(MembershipApplicationDetailRequest req, CancellationToken ct) {
        var app = _applicationRepo.Get(req.Id);
        if (app == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var chapterName = "";
        if (app.ChapterId.HasValue) {
            var chapter = _chapterRepo.Get(app.ChapterId.Value);
            if (chapter != null)
                chapterName = chapter.Name;
        }

        DueSelectionAdminDTO? dueDto = null;
        if (app.DueSelectionId.HasValue) {
            var ds = _dueSelectionRepo.Get(app.DueSelectionId.Value);
            if (ds != null) {
                dueDto = new DueSelectionAdminDTO {
                    Id = ds.Id,
                    FirstName = ds.FirstName,
                    LastName = ds.LastName,
                    EMail = ds.EMail,
                    SelectedDue = ds.SelectedDue,
                    ReducedAmount = ds.ReducedAmount,
                    ReducedJustification = ds.ReducedJustification,
                    SelectedValuation = (int)ds.SelectedValuation,
                    Status = (int)ds.Status,
                    ProcessedAt = ds.ProcessedAt
                };
            }
        }

        await SendAsync(new MembershipApplicationDetailDTO {
            Id = app.Id,
            FirstName = app.FirstName,
            LastName = app.LastName,
            DateOfBirth = app.DateOfBirth,
            Citizenship = app.Citizenship,
            EMail = app.EMail,
            PhoneNumber = app.PhoneNumber,
            AddressStreet = app.AddressStreet,
            AddressHouseNbr = app.AddressHouseNbr,
            AddressPostCode = app.AddressPostCode,
            AddressCity = app.AddressCity,
            ChapterId = app.ChapterId,
            ChapterName = chapterName,
            DueSelection = dueDto,
            ConformityDeclarationAccepted = app.ConformityDeclarationAccepted,
            HasPriorDeclinedApplication = app.HasPriorDeclinedApplication,
            IsMemberOfAnotherParty = app.IsMemberOfAnotherParty,
            ApplicationText = app.ApplicationText,
            EntryDate = app.EntryDate,
            SubmittedAt = app.SubmittedAt,
            Status = (int)app.Status,
            ProcessedAt = app.ProcessedAt,
            LinkedMotionId = _motionRepo.GetByLinkedApplicationId(app.Id)?.Id
        }, cancellation: ct);
    }
}
