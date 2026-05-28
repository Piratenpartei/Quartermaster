using System;
using System.Linq;
using System.Security.Cryptography;
using LinqToDB;

namespace Quartermaster.Data.Submissions;

public class PendingSubmissionRepository {
    /// <summary>Unconfirmed submissions older than this are swept; confirmed rows are also pruned after this window past confirmation.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    private readonly DbContext _context;

    public PendingSubmissionRepository(DbContext context) {
        _context = context;
    }

    public PendingSubmission Create(PendingSubmissionKind kind, string payloadJson, string email, DateTime now) {
        var row = new PendingSubmission {
            Token = GenerateToken(),
            Kind = kind,
            PayloadJson = payloadJson,
            Email = email,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
            ConfirmedAt = null
        };
        _context.Insert(row);
        return row;
    }

    public PendingSubmission? Get(string token) {
        return _context.GetTable<PendingSubmission>().FirstOrDefault(p => p.Token == token);
    }

    /// <summary>
    /// Atomically claims an unconfirmed, unexpired submission: sets ConfirmedAt if it was
    /// null and the row hasn't expired. Returns true only for the caller that won the claim,
    /// so concurrent confirm clicks can't materialize the entity twice.
    /// </summary>
    public bool TryClaim(string token, DateTime now) {
        var affected = _context.GetTable<PendingSubmission>()
            .Where(p => p.Token == token && p.ConfirmedAt == null && p.ExpiresAt >= now)
            .Set(p => p.ConfirmedAt, now)
            .Update();
        return affected == 1;
    }

    /// <summary>Deletes unconfirmed rows past expiry and confirmed rows past the same window after confirmation.</summary>
    public int PurgeStale(DateTime now) {
        var cutoff = now - Lifetime;
        return _context.GetTable<PendingSubmission>()
            .Where(p => (p.ConfirmedAt == null && p.ExpiresAt < now)
                || (p.ConfirmedAt != null && p.ConfirmedAt < cutoff))
            .Delete();
    }

    private static string GenerateToken() {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
