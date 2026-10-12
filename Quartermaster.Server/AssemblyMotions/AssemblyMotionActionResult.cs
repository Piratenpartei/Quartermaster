using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public record AssemblyMotionActionResult(int StatusCode, string? ErrorKey, AssemblyMotionDetailDTO? Detail) {
    public static AssemblyMotionActionResult NotFound { get; } = new(404, null, null);

    public static AssemblyMotionActionResult Done { get; } = new(204, null, null);

    public static AssemblyMotionActionResult Fail(int statusCode, string errorKey) => new(statusCode, errorKey, null);

    public static AssemblyMotionActionResult Ok(AssemblyMotionDetailDTO detail) => new(200, null, detail);
}
