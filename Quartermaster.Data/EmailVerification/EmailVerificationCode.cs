using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.EmailVerification;

/// <summary>
/// The current email verification code for one normalized address. Only a salted hash of the
/// code is stored.
/// </summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class EmailVerificationCode {
    public const string TableName = "EmailVerificationCodes";
    public const int EmailMaxLength = 320;
    public const int HashMaxLength = 64;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public string CodeSalt { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int SendCount { get; set; }
    public DateTime SendWindowStartedAt { get; set; }
}
