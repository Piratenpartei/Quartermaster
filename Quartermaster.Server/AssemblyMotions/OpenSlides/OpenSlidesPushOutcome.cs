using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.OpenSlides;

public record OpenSlidesPushOutcome(OpenSlidesPushResultDTO? Result, string? ErrorKey) {
    public static OpenSlidesPushOutcome Ok(OpenSlidesPushResultDTO result) {
        return new(result, null);
    }

    public static OpenSlidesPushOutcome Fail(string errorKey) {
        return new(null, errorKey);
    }
}
