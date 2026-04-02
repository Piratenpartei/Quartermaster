using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Quartermaster.Server.Authentication;

public class TokenAuthenticationHandler : AuthenticationHandler<TokenAuthenticationHandlerOptions> {
    public TokenAuthenticationHandler(IOptionsMonitor<TokenAuthenticationHandlerOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Task.FromResult(AuthenticateResult.NoResult());

        var tokenContent = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(tokenContent))
            return Task.FromResult(AuthenticateResult.Fail("Empty token"));

        var tokenRepository = Context.RequestServices.GetRequiredService<TokenRepository>();
        var token = tokenRepository.ValidateLoginToken(tokenContent);
        if (token == null || token.UserId == null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired token"));

        var userRepository = Context.RequestServices.GetRequiredService<UserRepository>();
        var user = userRepository.GetById(token.UserId.Value);
        if (user == null)
            return Task.FromResult(AuthenticateResult.Fail("User not found"));

        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username ?? user.EMail)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class TokenAuthenticationHandlerOptions : AuthenticationSchemeOptions {
    public const string DefaultScheme = "Token";
}
