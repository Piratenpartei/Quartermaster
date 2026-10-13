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
}
