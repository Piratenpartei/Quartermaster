using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.EndpointProcessors;

public class ChapterPermissionRequirement<TRequest> : IPreProcessor<TRequest>
    where TRequest : IChapterIdentifier {
    private readonly DbContext _context;

    public ChapterPermissionRequirement(DbContext context) {
        _context = context;
    }

    public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) {
        var epDefinition = ctx.HttpContext.GetEndpoint()?.Metadata.OfType<EndpointDefinition>().FirstOrDefault();
        if (epDefinition == null || epDefinition.AllowedPermissions == null || epDefinition.AllowedPermissions.Count == 0)
            throw new UnreachableException();

        if (!ctx.HttpContext.Request.Headers.TryGetValue("UserId", out var userIdStr)
            || !Guid.TryParse(userIdStr, out var userId)) {
            return ctx.HttpContext.Response.SendForbiddenAsync(ct);
        }

        if (!_context.ChapterPermissions.HasPermissionForChapter(userId, ctx.Request.ChapterId,
            epDefinition.AllowedPermissions[0])) {
            return ctx.HttpContext.Response.SendForbiddenAsync(ct);
        }

        return Task.CompletedTask;
    }
}