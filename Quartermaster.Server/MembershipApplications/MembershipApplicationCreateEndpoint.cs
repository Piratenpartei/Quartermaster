using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Server.MembershipApplications;

public class MembershipApplicationCreateEndpoint : Endpoint<MembershipApplicationDTO> {
    private readonly MembershipApplicationRepository _applicationRepository;
    private readonly DueSelectionRepository _dueSelectionRepository;

    public MembershipApplicationCreateEndpoint(
        MembershipApplicationRepository applicationRepository,
        DueSelectionRepository dueSelectionRepository) {
        _applicationRepository = applicationRepository;
        _dueSelectionRepository = dueSelectionRepository;
    }

    public override void Configure() {
        Post("/api/membershipapplications");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MembershipApplicationDTO req, CancellationToken ct) {
        Guid? dueSelectionId = null;
        if (req.DueSelection != null) {
            var dueSelection = DueSelectionMapper.FromDto(req.DueSelection);
            _dueSelectionRepository.Create(dueSelection);
            dueSelectionId = dueSelection.Id;
        }

        var application = MembershipApplicationMapper.FromDto(req);
        application.DueSelectionId = dueSelectionId;
        application.SubmittedAt = DateTime.UtcNow;
        _applicationRepository.Create(application);

        await SendOkAsync(ct);
    }
}
