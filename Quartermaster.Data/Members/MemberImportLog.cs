using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Members;

[Table(TableName, IsColumnAttributeRequired = false)]
public class MemberImportLog {
    public const string TableName = "MemberImportLogs";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ImportedAt { get; set; }
    public string FileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public int TotalRecords { get; set; }
    public int NewRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int ErrorCount { get; set; }
    public string? Errors { get; set; }
    public long DurationMs { get; set; }
}
