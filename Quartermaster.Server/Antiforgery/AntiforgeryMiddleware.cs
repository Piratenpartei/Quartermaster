using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Quartermaster.Server.Antiforgery;

public class AntiforgeryMiddleware {
    private readonly RequestDelegate _next;

    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase) {
        "GET", "HEAD", "OPTIONS", "TRACE"
    };

    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase) {
        "/api/users/SamlConsume"
    };

    public AntiforgeryMiddleware(RequestDelegate next) {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context) {
        if (SafeMethods.Contains(context.Request.Method)) {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/")) {
            await _next(context);
            return;
        }

        if (ExemptPaths.Contains(path)) {
            await _next(context);
            return;
        }

        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        if (!await antiforgery.IsRequestValidAsync(context)) {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Antiforgery token validation failed.");
            return;
        }

        await _next(context);
    }
}
