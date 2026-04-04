using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AdministrativeDivisions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AdminDivisionImportLog {
    public const string TableName = "AdminDivisionImportLogs";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ImportedAt { get; set; }
    public string FileHash { get; set; } = "";
    public int TotalRecords { get; set; }
    public int AddedRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int RemovedRecords { get; set; }
    public int RemappedRecords { get; set; }
    public int OrphanedRecords { get; set; }
    public int ErrorCount { get; set; }
    public string? Errors { get; set; }
    public long DurationMs { get; set; }
}
