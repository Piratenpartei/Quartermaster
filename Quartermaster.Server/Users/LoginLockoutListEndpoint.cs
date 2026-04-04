using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class LoginLockoutListEndpoint : EndpointWithoutRequest<LoginLockoutListResponse> {
    private readonly LoginAttemptRepository _loginAttemptRepository;
    private readonly OptionRepository _optionRepository;
    private readonly ChapterRepository _chapterRepository;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public LoginLockoutListEndpoint(
        LoginAttemptRepository loginAttemptRepository,
        OptionRepository optionRepository,
        ChapterRepository chapterRepository,
        UserGlobalPermissionRepository globalPermRepo) {
        _loginAttemptRepository = loginAttemptRepository;
        _optionRepository = optionRepository;
        _chapterRepository = chapterRepository;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/users/lockouts");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewUsers, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var maxAttempts = ParseIntOption("auth.lockout.max_attempts", 5);
        var durationMinutes = ParseIntOption("auth.lockout.duration_minutes", 15);
        var windowStart = DateTime.UtcNow.AddMinutes(-durationMinutes);

        var lockouts = _loginAttemptRepository.GetCurrentLockouts(windowStart, maxAttempts);

        var items = lockouts.Select(l => new LoginLockoutDTO {
            IpAddress = l.IpAddress,
            UsernameOrEmail = l.UsernameOrEmail,
            FailedAttempts = l.FailedAttempts,
            LastAttemptAt = l.LastAttemptAt,
            LockedUntil = l.LastAttemptAt.AddMinutes(durationMinutes)
        }).ToList();

        await SendAsync(new LoginLockoutListResponse { Items = items }, cancellation: ct);
    }

    private int ParseIntOption(string identifier, int fallback) {
        var value = _optionRepository.ResolveValue(identifier, null, _chapterRepository);
        if (int.TryParse(value, out var parsed) && parsed > 0)
            return parsed;
        return fallback;
    }
}
