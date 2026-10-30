using System;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.EmailVerification;

public class EmailVerificationCodeRepository {
    private readonly DbContext _context;

    public EmailVerificationCodeRepository(DbContext context) {
        _context = context;
    }

    public EmailVerificationCode? Get(string normalizedEmail) {
        var row = _context.GetTable<EmailVerificationCode>().FirstOrDefault(c => c.Email == normalizedEmail);
        return row != null && string.Equals(row.Email, normalizedEmail, StringComparison.Ordinal) ? row : null;
    }

    /// <summary>Inserts the row, or overwrites the existing row of the same address (its Id is kept).</summary>
    public void Upsert(EmailVerificationCode row) {
        var existing = Get(row.Email);
        if (existing == null) {
            _context.Insert(row);
            return;
        }
        row.Id = existing.Id;
        _context.Update(row);
    }

    public void Update(EmailVerificationCode row) {
        _context.Update(row);
    }

    /// <summary>Counts one wrong guess against the code with this hash; false when the code was replaced, consumed or already at the cap.</summary>
    public bool TryRegisterFailedAttempt(string normalizedEmail, string codeHash, int maxFailedAttempts) {
        return _context.GetTable<EmailVerificationCode>()
            .Where(c => c.Email == normalizedEmail && c.CodeHash == codeHash && c.FailedAttempts < maxFailedAttempts)
            .Set(c => c.FailedAttempts, c => c.FailedAttempts + 1)
            .Update() > 0;
    }

    /// <summary>Marks the code with this hash as verified; false when the code was replaced, consumed or already at the cap.</summary>
    public bool TryMarkVerified(string normalizedEmail, string codeHash, int maxFailedAttempts, DateTime verifiedAt, DateTime expiresAt) {
        return _context.GetTable<EmailVerificationCode>()
            .Where(c => c.Email == normalizedEmail && c.CodeHash == codeHash && c.FailedAttempts < maxFailedAttempts)
            .Set(c => c.VerifiedAt, verifiedAt)
            .Set(c => c.ExpiresAt, expiresAt)
            .Update() > 0;
    }

    public void Delete(string normalizedEmail) {
        _context.GetTable<EmailVerificationCode>().Where(c => c.Email == normalizedEmail).Delete();
    }

    public int DeleteExpiredBefore(DateTime cutoffUtc) {
        return _context.GetTable<EmailVerificationCode>().Where(c => c.ExpiresAt < cutoffUtc).Delete();
    }
}
