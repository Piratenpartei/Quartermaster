using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionInquiryCloseEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionInquiryIdRequest> {
    private readonly AssemblyMotionCommissionActions _actions;
    private readonly AssemblyMotionInquiryRepository _inquiries;
    private readonly AssemblyMotionInquiryService _service;

    public AssemblyMotionInquiryCloseEndpoint(AssemblyMotionCommissionActions actions,
        AssemblyMotionInquiryRepository inquiries, AssemblyMotionInquiryService service) {
        _actions = actions;
        _inquiries = inquiries;
        _service = service;
    }

    public override void Configure() {
        Post("/api/assemblymotions/inquiries/{Id}/close");
    }

    public override async Task HandleAsync(AssemblyMotionInquiryIdRequest req, CancellationToken ct) {
        var inquiry = _inquiries.Get(req.Id);
        if (inquiry == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var result = _actions.Run(inquiry.MotionId, (userId, nowUtc)
            => _service.Close(req.Id, userId, nowUtc));
        await SendResultAsync(result, ct);
    }
}
