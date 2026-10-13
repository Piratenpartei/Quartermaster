using System;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionUpvoteRemoveEndpoint : AssemblyMotionActionEndpoint {
    private readonly AssemblyMotionUpvoteService _service;

    public AssemblyMotionUpvoteRemoveEndpoint(AssemblyMotionMemberActions actions, AssemblyMotionUpvoteService service) : base(actions) {
        _service = service;
    }

    protected override bool RequiresEligibility => false;

    public override void Configure() {
        Delete("/api/assemblymotions/motions/{Id}/upvote");
    }

    protected override AssemblyMotionOutcome Execute(Guid motionId, Member member, DateTime nowUtc) {
        return _service.Remove(motionId, member, nowUtc);
    }
}
