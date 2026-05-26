using FastEndpoints;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Users;

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse> {
    private readonly UserRepository _userRepository;
    private readonly TokenRepository _tokenRepository;
    private readonly UserGlobalPermissionRepository _globalPermissionRepository;
    private readonly UserChapterPermissionRepository _chapterPermissionRepository;
    private readonly LoginAttemptRepository _loginAttemptRepository;
    private readonly OptionRepository _optionRepository;
    private readonly ChapterRepository _chapterRepository;

    public LoginEndpoint(UserRepository userRepository, TokenRepository tokenRepository,
        UserGlobalPermissionRepository globalPermissionRepository,
        UserChapterPermissionRepository chapterPermissionRepository,
        LoginAttemptRepository loginAttemptRepository,
        OptionRepository optionRepository,
        ChapterRepository chapterRepository) {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _globalPermissionRepository = globalPermissionRepository;
        _chapterPermissionRepository = chapterPermissionRepository;
        _loginAttemptRepository = loginAttemptRepository;
        _optionRepository = optionRepository;
        _chapterRepository = chapterRepository;
    }

    public override void Configure() {
        Post("/api/users/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct) {
        const string RndPw = "EE83C9600AA859921DC735E46DCAC5F83B7B1A7BDB0256524FEE6CFC9183930656F763FCB7D0AB" +
            "021CCB025F86F04EF0DC29DA022FA923576CE4FE832B78E850;031DAE440EF21E786C7ECF5B064C1B73;500000;SHA512";

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var identifier = req.Username ?? req.Email ?? "";

        // Lockout check
        var (maxAttempts, durationMinutes) = GetLockoutConfig();
        var windowStart = DateTime.UtcNow.AddMinutes(-durationMinutes);
        var recentFailures = _loginAttemptRepository.CountRecentFailures(ipAddress, identifier, windowStart);
        if (recentFailures >= maxAttempts) {
            var releaseAnchor = _loginAttemptRepository.GetLockoutReleaseAnchor(ipAddress, identifier, windowStart, maxAttempts);
            if (releaseAnchor.HasValue) {
                var retryAfter = releaseAnchor.Value.AddMinutes(durationMinutes) - DateTime.UtcNow;
                var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                HttpContext.Response.Headers["Retry-After"] = seconds.ToString();
            }
            await SendAsync(new LoginResponse(), statusCode: 429, cancellation: ct);
            return;
        }

        var user = _userRepository.GetByUsername(req.Username!);

        if (PasswordHasher.Verify(req.Password, user?.PasswordHash ?? RndPw) && user != null) {
            _loginAttemptRepository.LogAttempt(ipAddress, identifier, true);
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
            var token = _tokenRepository.LoginUser(user.Id, ipAddress, string.IsNullOrEmpty(userAgent) ? null : userAgent);

            var globalPermissions = _globalPermissionRepository.GetForUser(user.Id)
                .Select(p => p.Identifier)
                .ToList();

            var chapterPermissions = _chapterPermissionRepository.GetAllForUser(user.Id);

            AuthCookie.Set(HttpContext, token.Content, token.Expires);

            var response = new LoginResponse {
                User = new LoginUserInfo {
                    Id = user.Id,
                    Username = user.Username ?? "",
                    DisplayName = user.DisplayName(),
                    Email = user.Email
                },
                Permissions = new LoginPermissions {
                    Global = globalPermissions,
                    Chapters = chapterPermissions.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value)
                }
            };

            await SendAsync(response, cancellation: ct);
        } else {
            _loginAttemptRepository.LogAttempt(ipAddress, identifier, false);
            await SendUnauthorizedAsync(ct);
        }
    }

    private (int MaxAttempts, int DurationMinutes) GetLockoutConfig() {
        var maxAttempts = ParseIntOption("auth.lockout.max_attempts", 5);
        var durationMinutes = ParseIntOption("auth.lockout.duration_minutes", 15);
        return (maxAttempts, durationMinutes);
    }

    private int ParseIntOption(string identifier, int fallback) {
        var value = _optionRepository.ResolveValue(identifier, null, _chapterRepository);
        if (int.TryParse(value, out var parsed) && parsed > 0)
            return parsed;
        return fallback;
    }
}
