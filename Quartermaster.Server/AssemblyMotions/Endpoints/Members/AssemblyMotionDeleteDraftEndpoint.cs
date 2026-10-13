using System;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionDeleteDraftEndpoint : AssemblyMotionActionEndpoint {
    private readonly AssemblyMotionService _service;

    public AssemblyMotionDeleteDraftEndpoint(AssemblyMotionMemberActions actions, AssemblyMotionService service) : base(actions) {
        _service = service;
    }

    protected override bool RequiresEligibility => false;

    public override void Configure() {
        Delete("/api/assemblymotions/motions/{Id}");
    }

    protected override AssemblyMotionOutcome Execute(Guid motionId, Member member, DateTime nowUtc) {
        return _service.DeleteDraft(motionId, member, nowUtc);
    }
}
