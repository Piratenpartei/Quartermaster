using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public record AssemblyMotionActionResult(int StatusCode, string? ErrorKey, AssemblyMotionDetailDTO? Detail) {
    public static AssemblyMotionActionResult NotFound { get; } = new(404, null, null);

    public static AssemblyMotionActionResult Done { get; } = new(204, null, null);

    public static AssemblyMotionActionResult Fail(int statusCode, string errorKey) {
        return new(statusCode, errorKey, null);
    }

    public static AssemblyMotionActionResult Ok(AssemblyMotionDetailDTO detail) {
        return new(200, null, detail);
    }
}
