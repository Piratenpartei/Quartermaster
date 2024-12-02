using FastEndpoints;
using Quartermaster.Data.Chapters;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.EndpointProcessors;

/// <summary>
/// Ensures the User is part of the requested Chapter.
/// </summary>
public class ChapterRequirement<TRequest> : IPreProcessor<TRequest> where TRequest : IChapterIdentifier {
    public Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct) {
        if (!context.HttpContext.User.HasClaim("Chapter", context.Request.ChapterId.ToString()))
            return context.HttpContext.Response.SendForbiddenAsync(ct);

        return Task.CompletedTask;
    }
}