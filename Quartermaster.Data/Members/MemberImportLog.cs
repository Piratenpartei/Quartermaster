using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.Members;

namespace Quartermaster.Data.Members;

[Table(TableName, IsColumnAttributeRequired = false)]
public class MemberImportLog {
    public const string TableName = "MemberImportLogs";

    /// <summary>Sentinel <see cref="DurationMs"/> value while a row is still running; the worker overwrites it with the real duration on completion.</summary>
    public const long PendingDurationMs = -1;

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

    public MemberImportLogDTO ToDto() => new() {
        Id = Id,
        ImportedAt = ImportedAt.ToDtoUtc(),
        FileName = FileName,
        FileHash = FileHash,
        TotalRecords = TotalRecords,
        NewRecords = NewRecords,
        UpdatedRecords = UpdatedRecords,
        ErrorCount = ErrorCount,
        Errors = Errors,
        DurationMs = DurationMs
    };
}
