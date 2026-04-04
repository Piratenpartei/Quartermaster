using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Email;

[Table(TableName)]
public class EmailLog {
    public const string TableName = "EmailLogs";

    [Column, PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column]
    public string Recipient { get; set; } = "";

    [Column]
    public string Subject { get; set; } = "";

    [Column]
    public string? TemplateIdentifier { get; set; }

    [Column]
    public string? SourceEntityType { get; set; }

    [Column]
    public Guid? SourceEntityId { get; set; }

    [Column]
    public string Status { get; set; } = "Pending";

    [Column]
    public string? Error { get; set; }

    [Column]
    public int AttemptCount { get; set; }

    [Column]
    public DateTime CreatedAt { get; set; }

    [Column]
    public DateTime? SentAt { get; set; }

    [Column]
    public string? HtmlBody { get; set; }
}
