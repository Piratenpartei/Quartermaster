using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Tokens;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

/// <summary>Clears the auth cookie and revokes the underlying token row. Idempotent on anonymous requests.</summary>
public class LogoutEndpoint : EndpointWithoutRequest {
    private readonly TokenRepository _tokenRepo;

    public LogoutEndpoint(TokenRepository tokenRepo) {
        _tokenRepo = tokenRepo;
    }

    public override void Configure() {
        Post("/api/users/logout");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var tokenContent = ReadIncomingTokenContent();
        if (!string.IsNullOrEmpty(tokenContent)) {
            var token = _tokenRepo.ValidateLoginToken(tokenContent);
            if (token != null)
                _tokenRepo.DeleteToken(token.Id);
        }
        AuthCookie.Clear(HttpContext);
        await SendOkAsync(ct);
    }

    private string? ReadIncomingTokenContent() {
        if (HttpContext.Request.Cookies.TryGetValue(AuthCookie.Name, out var cookie) && !string.IsNullOrEmpty(cookie))
            return cookie;
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            return authHeader["Bearer ".Length..].Trim();
        return null;
    }
}
