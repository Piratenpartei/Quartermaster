using System;
using System.Threading.Tasks;
using Quartermaster.Data;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.LoginAttempts;

public class LockoutLogicTests {
    private WorkerDatabase _db = default!;
    private DbContext _context = default!;
    private LoginAttemptRepository _repo = default!;

    [Before(Test)]
    public void Setup() {
        _db = TestDatabaseFixture.Acquire();
        _db.CleanAllTables();
        _context = _db.CreateDbContext();
        _repo = new LoginAttemptRepository(_context);
    }

    [After(Test)]
    public void Teardown() {
        _context?.Dispose();
        TestDatabaseFixture.Release(_db);
    }

    [Test]
    public async Task CountRecentFailures_returns_zero_for_no_attempts() {
        var count = _repo.CountRecentFailures("1.2.3.4", "alice", DateTime.UtcNow.AddMinutes(-5));
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task CountRecentFailures_counts_only_failures_not_successes() {
        _repo.LogAttempt("1.2.3.4", "alice", success: false);
        _repo.LogAttempt("1.2.3.4", "alice", success: false);
        _repo.LogAttempt("1.2.3.4", "alice", success: true);
        var count = _repo.CountRecentFailures("1.2.3.4", "alice", DateTime.UtcNow.AddMinutes(-5));
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task CountRecentFailures_isolates_per_IP_plus_user_combo() {
        _repo.LogAttempt("1.2.3.4", "alice", success: false);
        _repo.LogAttempt("5.6.7.8", "alice", success: false);
        _repo.LogAttempt("1.2.3.4", "bob", success: false);

        await Assert.That(_repo.CountRecentFailures("1.2.3.4", "alice", DateTime.UtcNow.AddMinutes(-5))).IsEqualTo(1);
        await Assert.That(_repo.CountRecentFailures("5.6.7.8", "alice", DateTime.UtcNow.AddMinutes(-5))).IsEqualTo(1);
        await Assert.That(_repo.CountRecentFailures("1.2.3.4", "bob", DateTime.UtcNow.AddMinutes(-5))).IsEqualTo(1);
        await Assert.That(_repo.CountRecentFailures("5.6.7.8", "bob", DateTime.UtcNow.AddMinutes(-5))).IsEqualTo(0);
    }

    [Test]
    public async Task CountRecentFailures_respects_sliding_window() {
        _repo.LogAttempt("1.2.3.4", "alice", success: false);
        // Count in the next 1-minute window
        await Task.Delay(10);
        var recentWindow = DateTime.UtcNow.AddMilliseconds(-5);
        var count = _repo.CountRecentFailures("1.2.3.4", "alice", recentWindow);
        // The attempt we logged is just before the window, should not count
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task GetCurrentLockouts_returns_pairs_over_threshold() {
        for (var i = 0; i < 5; i++)
            _repo.LogAttempt("1.2.3.4", "alice", success: false);
        _repo.LogAttempt("5.6.7.8", "bob", success: false);

        var lockouts = _repo.GetCurrentLockouts(DateTime.UtcNow.AddMinutes(-5), maxAttempts: 5);
        await Assert.That(lockouts.Count).IsEqualTo(1);
        await Assert.That(lockouts[0].IpAddress).IsEqualTo("1.2.3.4");
        await Assert.That(lockouts[0].UsernameOrEmail).IsEqualTo("alice");
        await Assert.That(lockouts[0].FailedAttempts).IsEqualTo(5);
    }

    [Test]
    public async Task GetCurrentLockouts_excludes_pairs_with_recent_success() {
        for (var i = 0; i < 5; i++)
            _repo.LogAttempt("1.2.3.4", "alice", success: false);
        _repo.LogAttempt("1.2.3.4", "alice", success: true);

        var lockouts = _repo.GetCurrentLockouts(DateTime.UtcNow.AddMinutes(-5), maxAttempts: 5);
        await Assert.That(lockouts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetCurrentLockouts_exact_threshold_locks() {
        for (var i = 0; i < 5; i++)
            _repo.LogAttempt("1.2.3.4", "alice", success: false);
        var lockouts = _repo.GetCurrentLockouts(DateTime.UtcNow.AddMinutes(-5), maxAttempts: 5);
        await Assert.That(lockouts.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetCurrentLockouts_below_threshold_does_not_lock() {
        for (var i = 0; i < 4; i++)
            _repo.LogAttempt("1.2.3.4", "alice", success: false);
        var lockouts = _repo.GetCurrentLockouts(DateTime.UtcNow.AddMinutes(-5), maxAttempts: 5);
        await Assert.That(lockouts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ClearFailures_removes_only_failures() {
        for (var i = 0; i < 3; i++)
            _repo.LogAttempt("1.2.3.4", "alice", success: false);
        _repo.LogAttempt("1.2.3.4", "alice", success: true);

        _repo.ClearFailures("1.2.3.4", "alice");

        await Assert.That(_repo.CountRecentFailures("1.2.3.4", "alice", DateTime.UtcNow.AddMinutes(-5))).IsEqualTo(0);
    }

    [Test]
    public async Task ClearFailures_isolated_to_specific_IP_user_pair() {
        _repo.LogAttempt("1.2.3.4", "alice", success: false);
        _repo.LogAttempt("5.6.7.8", "alice", success: false);
        _repo.LogAttempt("1.2.3.4", "bob", success: false);

        _repo.ClearFailures("1.2.3.4", "alice");

        await Assert.That(_repo.CountRecentFailures("1.2.3.4", "alice", DateTime.UtcNow.AddMinutes(-5))).IsEqualTo(0);
        await Assert.That(_repo.CountRecentFailures("5.6.7.8", "alice", DateTime.UtcNow.AddMinutes(-5))).IsEqualTo(1);
        await Assert.That(_repo.CountRecentFailures("1.2.3.4", "bob", DateTime.UtcNow.AddMinutes(-5))).IsEqualTo(1);
    }
}
