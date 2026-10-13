using System;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionUpvoteEndpoint : AssemblyMotionActionEndpoint {
    private readonly AssemblyMotionUpvoteService _service;

    public AssemblyMotionUpvoteEndpoint(AssemblyMotionMemberActions actions, AssemblyMotionUpvoteService service) : base(actions) {
        _service = service;
    }

    protected override bool RequiresEligibility => true;

    public override void Configure() {
        Post("/api/assemblymotions/motions/{Id}/upvote");
    }

    protected override AssemblyMotionOutcome Execute(Guid motionId, Member member, DateTime nowUtc) {
        return _service.Upvote(motionId, member, nowUtc);
    }
}
