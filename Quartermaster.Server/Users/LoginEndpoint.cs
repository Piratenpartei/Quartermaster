using FastEndpoints;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
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

        var ipAddress = GetClientIp();
        var identifier = req.Username ?? req.EMail ?? "";

        // Lockout check
        var (maxAttempts, durationMinutes) = GetLockoutConfig();
        var windowStart = DateTime.UtcNow.AddMinutes(-durationMinutes);
        var recentFailures = _loginAttemptRepository.CountRecentFailures(ipAddress, identifier, windowStart);
        if (recentFailures >= maxAttempts) {
            await SendAsync(new LoginResponse(), statusCode: 429, cancellation: ct);
            return;
        }

        var user = _userRepository.GetByUsername(req.Username!);

        if (PasswordHashser.Verify(req.Password, user?.PasswordHash ?? RndPw) && user != null) {
            _loginAttemptRepository.LogAttempt(ipAddress, identifier, true);
            var token = _tokenRepository.LoginUser(user.Id);

            var globalPermissions = _globalPermissionRepository.GetForUser(user.Id)
                .Select(p => p.Identifier)
                .ToList();

            var chapterPermissions = _chapterPermissionRepository.GetAllForUser(user.Id);

            var response = new LoginResponse {
                Token = token.Content,
                Expires = token.Expires,
                User = new LoginUserInfo {
                    Id = user.Id,
                    Username = user.Username ?? "",
                    DisplayName = BuildDisplayName(user),
                    EMail = user.EMail
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

    private string GetClientIp() {
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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

    private static string BuildDisplayName(Data.Users.User user) {
        if (!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName))
            return $"{user.FirstName} {user.LastName}";

        if (!string.IsNullOrEmpty(user.Username))
            return user.Username;

        return user.EMail;
    }
}
