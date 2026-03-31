using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Data.ChapterAssociates;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerAddEndpoint : Endpoint<ChapterOfficerAddRequest> {
    private readonly ChapterOfficerRepository _officerRepo;

    public ChapterOfficerAddEndpoint(ChapterOfficerRepository officerRepo) {
        _officerRepo = officerRepo;
    }

    public override void Configure() {
        Post("/api/chapterofficers");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterOfficerAddRequest req, CancellationToken ct) {
        _officerRepo.Create(new ChapterOfficer {
            MemberId = req.MemberId,
            ChapterId = req.ChapterId,
            AssociateType = (ChapterOfficerType)req.AssociateType
        });

        await SendOkAsync(ct);
    }
}
