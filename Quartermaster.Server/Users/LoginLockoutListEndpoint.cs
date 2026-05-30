using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class LoginLockoutListEndpoint : EndpointWithoutRequest<LoginLockoutListResponse> {
    private readonly LoginAttemptRepository _loginAttemptRepository;
    private readonly OptionRepository _optionRepository;
    private readonly ChapterRepository _chapterRepository;
    private readonly PermissionContext _perms;

    public LoginLockoutListEndpoint(
        LoginAttemptRepository loginAttemptRepository,
        OptionRepository optionRepository,
        ChapterRepository chapterRepository,
        PermissionContext perms) {
        _loginAttemptRepository = loginAttemptRepository;
        _optionRepository = optionRepository;
        _chapterRepository = chapterRepository;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/lockouts");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewUsers)) {
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
            LastAttemptAt = l.LastAttemptAt.ToDtoUtc(),
            LockedUntil = l.LastAttemptAt.AddMinutes(durationMinutes).ToDtoUtc()
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
