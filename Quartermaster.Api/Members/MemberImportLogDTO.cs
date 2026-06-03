using System;

namespace Quartermaster.Api.Members;

public class MemberImportLogDTO {
    public Guid Id { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public string FileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public int TotalRecords { get; set; }
    public int NewRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int SkippedRecords { get; set; }
    public int NewFloatingRecords { get; set; }
    public int UpdatedFloatingRecords { get; set; }
    public int PromotedRecords { get; set; }
    public int ErrorCount { get; set; }
    public string? Errors { get; set; }
    public long DurationMs { get; set; }

    /// <summary>True while the import is still running; the worker updates this row once the work finishes (sentinel: <see cref="DurationMs"/> = -1).</summary>
    public bool IsPending => DurationMs < 0;
}
