using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.AssemblyMotions.OpenSlides;

public interface IOpenSlidesClient {
    Task LoginAsync(OpenSlidesSettings settings, CancellationToken ct);

    /// <summary>Returns the id of the created category.</summary>
    Task<int> CreateCategoryAsync(OpenSlidesSettings settings, int meetingId, string name, string prefix, CancellationToken ct);

    /// <summary>Returns the id of the created motion.</summary>
    Task<int> CreateMotionAsync(OpenSlidesSettings settings, OpenSlidesMotionCreate motion, CancellationToken ct);

    Task UpdateMotionAsync(OpenSlidesSettings settings, OpenSlidesMotionUpdate motion, CancellationToken ct);

    /// <summary>The current access token and refresh cookie for the autoupdate stream; logs in if needed.</summary>
    Task<OpenSlidesSessionTokens> GetSessionTokensAsync(OpenSlidesSettings settings, CancellationToken ct);

    /// <summary>An autoupdate response may carry a refreshed access token; it replaces the stored one.</summary>
    void ReportRefreshedToken(string authentication);

    /// <summary>Always <c>pseudoanonymous</c>. Returns the id of the created poll.</summary>
    Task<int> CreatePollAsync(OpenSlidesSettings settings, OpenSlidesPollCreate poll, CancellationToken ct);

    Task StartPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);

    Task StopPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);

    Task PublishPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);

    Task ResetPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);

    Task DeletePollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct);

    /// <summary><paramref name="countdownTime"/> is the absolute end as Unix seconds while running, the remaining seconds otherwise.</summary>
    Task UpdateCountdownAsync(OpenSlidesSettings settings, int countdownId, bool running, double countdownTime, CancellationToken ct);

    /// <summary>Returns the id of the created topic.</summary>
    Task<int> CreateTopicAsync(OpenSlidesSettings settings, int meetingId, string title, int agendaParentId, CancellationToken ct);

    Task ProjectAsync(OpenSlidesSettings settings, int meetingId, IReadOnlyList<int> projectorIds, string contentObjectId, string? type, CancellationToken ct);
}
