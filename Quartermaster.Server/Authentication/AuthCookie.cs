using System;
using Microsoft.AspNetCore.Http;

namespace Quartermaster.Server.Authentication;

/// <summary>HttpOnly + Secure + SameSite=Strict cookie carrying the bearer token to browser clients.</summary>
public static class AuthCookie {
    public const string Name = ".Quartermaster.Auth";
    // Site-wide path: the cookie has to ride along on both /api endpoints AND the SignalR
    // /hubs WebSocket upgrade — these don't share a tighter prefix. HttpOnly keeps it
    // out of JS reach so the broader path doesn't widen the attack surface.
    private const string Path = "/";

    public static void Set(HttpContext ctx, string token, DateTime? expires) {
        ctx.Response.Cookies.Append(Name, token, BuildOptions(ctx, expires));
    }

    public static void Clear(HttpContext ctx) {
        ctx.Response.Cookies.Delete(Name, new CookieOptions { Path = Path });
    }

    // Secure tracks the request scheme so the cookie is HTTPS-only in production (where
    // UseHttpsRedirection guarantees HTTPS) but still flows over the TestServer's HTTP.
    private static CookieOptions BuildOptions(HttpContext ctx, DateTime? expires) => new() {
        HttpOnly = true,
        Secure = ctx.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires
    };
}
