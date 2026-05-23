using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Saml;

/// <summary>
/// Replay-tracking record for SAML assertions. Each row pins one previously-consumed AssertionID;
/// duplicate AssertionIDs are rejected at insert time via the unique constraint on
/// <see cref="AssertionId"/>. Rows older than <see cref="ExpiresAt"/> are safe to prune.
/// </summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class UsedSamlAssertion {
    public const string TableName = "UsedSamlAssertions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string AssertionId { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}
