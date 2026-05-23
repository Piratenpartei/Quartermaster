using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data;
using Quartermaster.Data.Saml;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Saml;

public class UsedSamlAssertionRepositoryTests : IDisposable {
    private DbContext _context = default!;
    private UsedSamlAssertionRepository _repo = default!;

    [Before(Test)]
    public void Setup() {
        TestDatabaseFixture.CleanAllTables();
        _context = TestDatabaseFixture.CreateDbContext();
        _repo = new UsedSamlAssertionRepository(_context);
    }

    [Test]
    public async Task First_use_of_assertion_id_is_recorded_and_returns_true() {
        var ok = _repo.TryMarkUsed("_fresh-1", DateTime.UtcNow.AddMinutes(5));
        await Assert.That(ok).IsTrue();
        await Assert.That(_context.UsedSamlAssertions.Any(a => a.AssertionId == "_fresh-1")).IsTrue();
    }

    [Test]
    public async Task Second_use_of_same_assertion_id_returns_false_replay_detected() {
        var first = _repo.TryMarkUsed("_replay-1", DateTime.UtcNow.AddMinutes(5));
        var second = _repo.TryMarkUsed("_replay-1", DateTime.UtcNow.AddMinutes(5));
        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    [Test]
    public async Task Expired_rows_are_pruned_on_next_call() {
        _context.Insert(new UsedSamlAssertion {
            AssertionId = "_stale",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            UsedAt = DateTime.UtcNow.AddMinutes(-10)
        });
        var ok = _repo.TryMarkUsed("_brand-new", DateTime.UtcNow.AddMinutes(5));
        await Assert.That(ok).IsTrue();
        await Assert.That(_context.UsedSamlAssertions.Any(a => a.AssertionId == "_stale")).IsFalse();
    }

    [Test]
    public async Task Distinct_assertion_ids_each_succeed() {
        await Assert.That(_repo.TryMarkUsed("_a", DateTime.UtcNow.AddMinutes(5))).IsTrue();
        await Assert.That(_repo.TryMarkUsed("_b", DateTime.UtcNow.AddMinutes(5))).IsTrue();
        await Assert.That(_repo.TryMarkUsed("_c", DateTime.UtcNow.AddMinutes(5))).IsTrue();
    }

    public void Dispose() {
        _context?.Dispose();
    }
}
