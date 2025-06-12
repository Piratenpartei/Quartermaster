using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartermaster.Data;
using Quartermaster.Data.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Quartermaster.Server.Authentication;

public class TokenAuthenticationHandler : AuthenticationHandler<TokenAuthenticationHandlerOptions> {
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public TokenAuthenticationHandler(IOptionsMonitor<TokenAuthenticationHandlerOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, IServiceScopeFactory serviceScopeFactory)
        : base(options, logger, encoder) {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        if (IsPublicEndpoint())
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.TryGetValue(Options.TokenHeaderName, out var token)
            || string.IsNullOrEmpty(token)
            || !Request.Headers.TryGetValue(Options.UserIdHeaderName, out var userIdStr)) {
            return Task.FromResult(AuthenticateResult.Fail(""));
        }

        if (!Guid.TryParse(userIdStr, out var userId))
            return Task.FromResult(AuthenticateResult.Fail(""));

        using var scope = _serviceScopeFactory.CreateScope();
        var tokenRepository = scope.Resolve<TokenRepository>();

        // token is implicitely converted to string?, if the IsNullOrEmpty check above doesn't fail
        // it won't be null here either.
        if (!tokenRepository.CheckLoginToken(token!, userId, ""))
            return Task.FromResult(AuthenticateResult.Fail(""));

        var claims = new List<Claim>();

        var claimsIdentity = new ClaimsIdentity(claims, Scheme.Name);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name)));
    }

    private bool IsPublicEndpoint()
        => Context.GetEndpoint()?
              .Metadata.OfType<EndpointDefinition>().FirstOrDefault()?
              .AnonymousVerbs?.Length > 0;
}

public class TokenAuthenticationHandlerOptions : AuthenticationSchemeOptions {
    public const string DefaultScheme = "Token";
    public string TokenHeaderName { get; set; } = "AuthToken";
    public string UserIdHeaderName { get; set; } = "UserId";
}