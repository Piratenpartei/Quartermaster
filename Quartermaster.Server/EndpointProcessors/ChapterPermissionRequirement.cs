using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.UserChapterPermissions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.EndpointProcessors;

public class ChapterPermissionRequirement<TRequest> : IPreProcessor<TRequest>
    where TRequest : IChapterIdentifier {
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ChapterPermissionRequirement(IServiceScopeFactory scopeFactory) {
        _serviceScopeFactory = scopeFactory;
    }

    public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) {
        var epDefinition = ctx.HttpContext.GetEndpoint()?.Metadata.OfType<EndpointDefinition>().FirstOrDefault();
        if (epDefinition == null || epDefinition.AllowedPermissions == null || epDefinition.AllowedPermissions.Count == 0)
            throw new UnreachableException();

        if (ctx.Request == null)
            return ctx.HttpContext.Response.SendErrorsAsync([], cancellation: ct);

        if (!ctx.HttpContext.Request.Headers.TryGetValue("UserId", out var userIdStr)
            || !Guid.TryParse(userIdStr, out var userId)) {
            return ctx.HttpContext.Response.SendForbiddenAsync(ct);
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var userChapterPermissionRepository = scope.Resolve<UserChapterPermissionRepository>();

        if (!userChapterPermissionRepository.HasPermissionForChapter(userId, ctx.Request.ChapterId,
            epDefinition.AllowedPermissions[0])) {
            return ctx.HttpContext.Response.SendForbiddenAsync(ct);
        }

        return Task.CompletedTask;
    }
}