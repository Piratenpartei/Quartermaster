using System;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionJoinEndpoint : AssemblyMotionActionEndpoint {
    private readonly AssemblyMotionService _service;

    public AssemblyMotionJoinEndpoint(AssemblyMotionMemberActions actions, AssemblyMotionService service) : base(actions) {
        _service = service;
    }

    protected override bool RequiresEligibility => true;

    public override void Configure() {
        Post("/api/assemblymotions/motions/{Id}/join");
    }

    protected override AssemblyMotionOutcome Execute(Guid motionId, Member member, DateTime nowUtc) {
        return _service.Join(motionId, member, nowUtc);
    }
}
