using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions;

public record EligibilityResult(EligibilityProblem Problem, Member? Member) {
    public bool IsEligible => Problem == EligibilityProblem.None;
}
