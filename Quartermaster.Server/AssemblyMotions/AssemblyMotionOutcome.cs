using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public record AssemblyMotionOutcome(int StatusCode, string? ErrorKey, AssemblyMotion? Motion) {
    public bool Succeeded => ErrorKey == null;

    public static AssemblyMotionOutcome Ok(AssemblyMotion motion) {
        return new(200, null, motion);
    }

    public static AssemblyMotionOutcome Fail(int statusCode, string errorKey) {
        return new(statusCode, errorKey, null);
    }
}
