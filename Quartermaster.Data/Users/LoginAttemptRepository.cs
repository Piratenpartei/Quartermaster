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
            .Where(a => a.IpAddress == ipAddress
                && a.UsernameOrEmail == usernameOrEmail
                && !a.Success
                && a.AttemptedAt >= since)
            .Count();
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

public class LockedOutEntry {
    public string IpAddress { get; set; } = "";
    public string UsernameOrEmail { get; set; } = "";
    public int FailedAttempts { get; set; }
    public DateTime LastAttemptAt { get; set; }
    public bool HasRecentSuccess { get; set; }
}
