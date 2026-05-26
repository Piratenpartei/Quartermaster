using System;
using System.Threading.Tasks;
using Quartermaster.Data;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.LoginAttempts;

public class LockoutLogicTests : RepositoryTestBase {
    private DbContext _context = default!;
    private LoginAttemptRepository _repo = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _repo = new LoginAttemptRepository(_context);
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
        // Window start far in the future → the attempt is definitely outside.
        // Previously raced a 10 ms Task.Delay against a 5 ms window edge and
        // intermittently saw the attempt as still inside the window.
        var futureWindow = DateTime.UtcNow.AddDays(1);
        var count = _repo.CountRecentFailures("1.2.3.4", "alice", futureWindow);
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
