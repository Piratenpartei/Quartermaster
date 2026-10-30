using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Quartermaster.Data.EmailVerification;
using Quartermaster.Data.Options;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.MembershipApplications.EmailCodes;

public class EmailCodeService {
    public const int MaxFailedAttempts = 5;
    public const int CodeLength = 6;
    public const string LifetimeOption = "membership_application.email_code.lifetime_minutes";
    public const string SendsPerHourOption = "membership_application.email_code.sends_per_address_per_hour";

    private const int DefaultLifetimeMinutes = 15;
    private const int DefaultSendsPerHour = 10;
    private const int SaltLength = 16;
    private static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);

    private readonly EmailVerificationCodeRepository _repository;
    private readonly OptionRepository _options;

    public EmailCodeService(EmailVerificationCodeRepository repository, OptionRepository options) {
        _repository = repository;
        _options = options;
    }

    public static string Normalize(string email) {
        return email.Trim().ToLowerInvariant();
    }

    public int LifetimeMinutes() {
        return ReadPositive(LifetimeOption, DefaultLifetimeMinutes);
    }

    public EmailCodeIssueResult Issue(string email, DateTime nowUtc) {
        var normalized = Normalize(email);
        var row = _repository.Get(normalized);

        var windowStart = nowUtc;
        var sendCount = 0;
        if (row != null && nowUtc - row.SendWindowStartedAt < SendWindow) {
            windowStart = row.SendWindowStartedAt;
            sendCount = row.SendCount;
        }

        if (sendCount >= ReadPositive(SendsPerHourOption, DefaultSendsPerHour))
            return new EmailCodeIssueResult(null, true);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        _repository.Upsert(new EmailVerificationCode {
            Email = normalized,
            CodeHash = Hash(salt, code),
            CodeSalt = Convert.ToBase64String(salt),
            ExpiresAt = nowUtc.AddMinutes(LifetimeMinutes()),
            FailedAttempts = 0,
            VerifiedAt = null,
            SendCount = sendCount + 1,
            SendWindowStartedAt = windowStart
        });
        return new EmailCodeIssueResult(code, false);
    }

    public EmailCodeCheckResult Check(string email, string code, DateTime nowUtc) {
        var row = _repository.Get(Normalize(email));
        if (row == null || row.ExpiresAt < nowUtc)
            return EmailCodeCheckResult.Expired;
        if (row.FailedAttempts >= MaxFailedAttempts)
            return EmailCodeCheckResult.Locked;

        if (!IsWellFormed(code) || !Matches(row, code))
            return _repository.TryRegisterFailedAttempt(row.Email, row.CodeHash, MaxFailedAttempts)
                ? EmailCodeCheckResult.Wrong
                : Unchanged(row.Email, row.CodeHash);

        var verified = _repository.TryMarkVerified(row.Email, row.CodeHash, MaxFailedAttempts, nowUtc, nowUtc.AddMinutes(LifetimeMinutes()));
        return verified ? EmailCodeCheckResult.Valid : Unchanged(row.Email, row.CodeHash);
    }

    public static bool IsAcceptableAddress(string email) {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        if (email.Trim().Length > EmailVerificationCode.EmailMaxLength)
            return false;
        return EmailAddressFormat.IsPlausible(email);
    }

    public void Consume(string email) {
        _repository.Delete(Normalize(email));
    }

    private EmailCodeCheckResult Unchanged(string normalizedEmail, string codeHash) {
        var current = _repository.Get(normalizedEmail);
        if (current == null || current.CodeHash != codeHash)
            return EmailCodeCheckResult.Expired;
        return EmailCodeCheckResult.Locked;
    }

    private static bool IsWellFormed(string? code) {
        return code != null && code.Length == CodeLength && code.All(char.IsAsciiDigit);
    }

    private static bool Matches(EmailVerificationCode row, string code) {
        var expected = Encoding.ASCII.GetBytes(row.CodeHash);
        var actual = Encoding.ASCII.GetBytes(Hash(Convert.FromBase64String(row.CodeSalt), code));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string Hash(byte[] salt, string code) {
        var codeBytes = Encoding.UTF8.GetBytes(code);
        var input = new byte[salt.Length + codeBytes.Length];
        salt.CopyTo(input, 0);
        codeBytes.CopyTo(input, salt.Length);
        return Convert.ToBase64String(SHA256.HashData(input));
    }

    private int ReadPositive(string identifier, int fallback) {
        var raw = _options.GetGlobalValue(identifier)?.Value;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            return value;
        return fallback;
    }
}
