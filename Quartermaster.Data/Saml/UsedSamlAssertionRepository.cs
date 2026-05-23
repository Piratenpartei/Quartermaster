using System;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Saml;

public class UsedSamlAssertionRepository {
    private readonly DbContext _context;

    public UsedSamlAssertionRepository(DbContext context) {
        _context = context;
    }

    /// <summary>
    /// Records the AssertionID as consumed. Returns <c>false</c> when the AssertionID has
    /// already been seen — the caller must reject the SAML response as a replay. The unique
    /// constraint on <c>AssertionId</c> remains the integrity backstop if two parallel
    /// requests race past the lookup. Lazily prunes rows past <see cref="UsedSamlAssertion.ExpiresAt"/>.
    /// </summary>
    public bool TryMarkUsed(string assertionId, DateTime expiresAt) {
        PruneExpired();
        var existing = _context.UsedSamlAssertions.Where(a => a.AssertionId == assertionId).FirstOrDefault();
        if (existing != null)
            return false;

        _context.Insert(new UsedSamlAssertion {
            AssertionId = assertionId,
            ExpiresAt = expiresAt
        });
        return true;
    }

    private void PruneExpired() {
        _context.UsedSamlAssertions.Where(a => a.ExpiresAt < DateTime.UtcNow).Delete();
    }
}
