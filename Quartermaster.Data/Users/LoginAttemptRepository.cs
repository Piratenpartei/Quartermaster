using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Users;

public class LoginAttemptRepository {
    private readonly DbContext _context;

    public LoginAttemptRepository(DbContext context) {
        _context = context;
    }

    public void LogAttempt(string ipAddress, string usernameOrEmail, bool success) {
        _context.Insert(new LoginAttempt {
            IpAddress = ipAddress,
            UsernameOrEmail = usernameOrEmail,
            Success = success,
            AttemptedAt = DateTime.UtcNow
        });
    }

    public int CountRecentFailures(string ipAddress, string usernameOrEmail, DateTime since) {
        return _context.LoginAttempts
            .Count(a => a.IpAddress == ipAddress
                && a.UsernameOrEmail == usernameOrEmail
                && !a.Success
                && a.AttemptedAt >= since);
    }

    /// <summary>
    /// Returns the timestamp of the failure whose expiry first releases a lockout — the
    /// <paramref name="maxAttempts"/>-th most recent failed attempt in the window. When that
    /// row's age exceeds the lockout duration, the count drops below the threshold and the
    /// user can retry. Returns null when fewer than <paramref name="maxAttempts"/> failures exist.
    /// </summary>
    public DateTime? GetLockoutReleaseAnchor(string ipAddress, string usernameOrEmail, DateTime since, int maxAttempts) {
        var timestamps = _context.LoginAttempts
            .Where(a => a.IpAddress == ipAddress
                && a.UsernameOrEmail == usernameOrEmail
                && !a.Success
                && a.AttemptedAt >= since)
            .OrderByDescending(a => a.AttemptedAt)
            .Take(maxAttempts)
            .Select(a => a.AttemptedAt)
            .ToList();
        if (timestamps.Count < maxAttempts)
            return null;
        return timestamps[^1];
    }

    /// <summary>
    /// Returns all (IpAddress, UsernameOrEmail) pairs currently locked out — i.e., with at least
    /// maxAttempts failed attempts since windowStart AND no successful attempt since the lockout began.
    /// </summary>
    public List<LockedOutEntry> GetCurrentLockouts(DateTime windowStart, int maxAttempts) {
        var recentAttempts = _context.LoginAttempts
            .Where(a => a.AttemptedAt >= windowStart)
            .ToList();

        var grouped = recentAttempts
            .GroupBy(a => new { a.IpAddress, a.UsernameOrEmail })
            .Select(g => new LockedOutEntry {
                IpAddress = g.Key.IpAddress,
                UsernameOrEmail = g.Key.UsernameOrEmail,
                FailedAttempts = g.Count(a => !a.Success),
                LastAttemptAt = g.Max(a => a.AttemptedAt),
                HasRecentSuccess = g.Any(a => a.Success)
            })
            .Where(e => e.FailedAttempts >= maxAttempts && !e.HasRecentSuccess)
            .OrderByDescending(e => e.LastAttemptAt)
            .ToList();

        return grouped;
    }

    /// <summary>
    /// Clears failed attempts for an IP+user pair, effectively unlocking them.
    /// </summary>
    public void ClearFailures(string ipAddress, string usernameOrEmail) {
        _context.LoginAttempts
            .Where(a => a.IpAddress == ipAddress
                && a.UsernameOrEmail == usernameOrEmail
                && !a.Success)
            .Delete();
    }
}
