using System;

namespace Quartermaster.Api.Members;

public class MemberImportLogDTO {
    public Guid Id { get; set; }
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
