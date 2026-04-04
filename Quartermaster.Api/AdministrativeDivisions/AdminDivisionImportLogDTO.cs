using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AdministrativeDivisions;

public class AdminDivisionImportLogDTO {
    public Guid Id { get; set; }
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

public class AdminDivisionImportLogListResponse {
    public List<AdminDivisionImportLogDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
