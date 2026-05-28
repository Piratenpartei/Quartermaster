using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.Submissions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Submissions;

public class PendingSubmissionRepositoryTests : RepositoryTestBase {
    private PendingSubmissionRepository _repo = default!;

    [Before(Test)]
    public void Setup() {
        _repo = new PendingSubmissionRepository(Db);
    }

    [Test]
    public async Task Create_sets_expiry_and_token() {
        var now = new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);
        var row = _repo.Create(PendingSubmissionKind.Motion, "{}", "a@t.local", now);
        await Assert.That(row.Token.Length).IsGreaterThan(20);
        await Assert.That(row.ExpiresAt).IsEqualTo(now + PendingSubmissionRepository.Lifetime);
        await Assert.That(row.ConfirmedAt).IsNull();
    }

    [Test]
    public async Task TryClaim_succeeds_once_then_fails() {
        var now = new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);
        var row = _repo.Create(PendingSubmissionKind.Motion, "{}", "a@t.local", now);

        await Assert.That(_repo.TryClaim(row.Token, now.AddMinutes(1))).IsTrue();
        await Assert.That(_repo.TryClaim(row.Token, now.AddMinutes(2))).IsFalse();
    }

    [Test]
    public async Task TryClaim_fails_when_expired() {
        var now = new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);
        var row = _repo.Create(PendingSubmissionKind.Motion, "{}", "a@t.local", now);
        var afterExpiry = now + PendingSubmissionRepository.Lifetime + TimeSpan.FromMinutes(1);
        await Assert.That(_repo.TryClaim(row.Token, afterExpiry)).IsFalse();
    }

    [Test]
    public async Task PurgeStale_removes_expired_unconfirmed_and_old_confirmed_only() {
        var now = new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);

        var fresh = _repo.Create(PendingSubmissionKind.Motion, "{}", "a@t.local", now);
        var expired = _repo.Create(PendingSubmissionKind.Motion, "{}", "b@t.local", now - TimeSpan.FromHours(50));
        var recentlyConfirmed = _repo.Create(PendingSubmissionKind.Motion, "{}", "c@t.local", now - TimeSpan.FromHours(1));
        _repo.TryClaim(recentlyConfirmed.Token, now);
        var oldConfirmed = _repo.Create(PendingSubmissionKind.Motion, "{}", "d@t.local", now - TimeSpan.FromHours(100));
        _repo.TryClaim(oldConfirmed.Token, now - TimeSpan.FromHours(60));

        var purged = _repo.PurgeStale(now);

        await Assert.That(purged).IsEqualTo(2);
        await Assert.That(_repo.Get(fresh.Token)).IsNotNull();
        await Assert.That(_repo.Get(expired.Token)).IsNull();
        await Assert.That(_repo.Get(recentlyConfirmed.Token)).IsNotNull();
        await Assert.That(_repo.Get(oldConfirmed.Token)).IsNull();
    }
}
