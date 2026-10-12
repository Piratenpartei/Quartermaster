using System;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionLeaveEndpoint : AssemblyMotionActionEndpoint {
    private readonly AssemblyMotionService _service;

    public AssemblyMotionLeaveEndpoint(AssemblyMotionMemberActions actions, AssemblyMotionService service) : base(actions) {
        _service = service;
    }

    protected override bool RequiresEligibility => false;

    public override void Configure() {
        Post("/api/assemblymotions/motions/{Id}/leave");
    }

    protected override AssemblyMotionOutcome Execute(Guid motionId, Member member, DateTime nowUtc)
        => _service.Leave(motionId, member, nowUtc);
}
