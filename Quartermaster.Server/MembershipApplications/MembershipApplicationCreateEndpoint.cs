using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Markdig;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.MembershipApplications;

public class MembershipApplicationCreateEndpoint : Endpoint<MembershipApplicationDTO> {
    private readonly MembershipApplicationRepository _applicationRepository;
    private readonly DueSelectionRepository _dueSelectionRepository;
    private readonly MotionRepository _motionRepo;

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public MembershipApplicationCreateEndpoint(
        MembershipApplicationRepository applicationRepository,
        DueSelectionRepository dueSelectionRepository,
        MotionRepository motionRepo) {
        _applicationRepository = applicationRepository;
        _dueSelectionRepository = dueSelectionRepository;
        _motionRepo = motionRepo;
    }

    public override void Configure() {
        Post("/api/membershipapplications");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MembershipApplicationDTO req, CancellationToken ct) {
        Guid? dueSelectionId = null;
        var isReduced = false;
        if (req.DueSelection != null) {
            var dueSelection = DueSelectionMapper.FromDto(req.DueSelection);
            isReduced = dueSelection.SelectedValuation == SelectedValuation.Reduced;
            dueSelection.Status = isReduced
                ? DueSelectionStatus.Pending
                : DueSelectionStatus.AutoApproved;
            _dueSelectionRepository.Create(dueSelection);
            dueSelectionId = dueSelection.Id;
        }

        var application = MembershipApplicationMapper.FromDto(req);
        application.DueSelectionId = dueSelectionId;
        application.SubmittedAt = DateTime.UtcNow;
        application.Status = ApplicationStatus.Pending;
        _applicationRepository.Create(application);

        // Spawn a single linked motion for chapter approval
        if (application.ChapterId.HasValue) {
            var md = $"**Mitgliedsantrag von {application.FirstName} {application.LastName}**\n\n"
                + $"- **E-Mail:** {application.EMail}\n"
                + $"- **Adresse:** {application.AddressStreet} {application.AddressHouseNbr}, "
                + $"{application.AddressPostCode} {application.AddressCity}\n";

            if (isReduced && req.DueSelection != null) {
                md += $"\n---\n\n"
                    + $"**Antrag auf Beitragsminderung**\n\n"
                    + $"- **Gewünschter Betrag:** {req.DueSelection.ReducedAmount}€\n"
                    + $"- **Begründung:** {req.DueSelection.ReducedJustification}\n"
                    + $"\n[Einstufung ansehen](/Administration/DueSelections/{dueSelectionId})\n";
            }

            md += $"\n[Antrag ansehen](/Administration/MembershipApplications/{application.Id})\n";

            var title = isReduced
                ? $"Mitgliedsantrag + Beitragsminderung: {application.FirstName} {application.LastName}"
                : $"Mitgliedsantrag: {application.FirstName} {application.LastName}";

            var motion = new Motion {
                ChapterId = application.ChapterId.Value,
                AuthorName = $"{application.FirstName} {application.LastName}",
                AuthorEMail = application.EMail,
                Title = title,
                Text = Markdown.ToHtml(md, MarkdownPipeline),
                IsPublic = false,
                LinkedMembershipApplicationId = application.Id,
                LinkedDueSelectionId = isReduced ? dueSelectionId : null,
                ApprovalStatus = MotionApprovalStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _motionRepo.Create(motion);
        }

        await SendOkAsync(ct);
    }
}
