using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;

namespace Quartermaster.Server.DueSelector;

public class DueSelectionCreateEndpoint : Endpoint<DueSelectionDTO> {
    private readonly DueSelectionRepository _dueSelectionRepository;

    public DueSelectionCreateEndpoint(DueSelectionRepository dueSelectionRepository) {
        _dueSelectionRepository = dueSelectionRepository;
    }

    public override void Configure() {
        Post("/api/dueselector");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DueSelectionDTO req, CancellationToken ct) {
        _dueSelectionRepository.Create(DueSelectionMapper.FromDto(req));
        await SendOkAsync(ct);
    }
}