using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Receipts;

public class UserReceiptDTO {
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UserReceiptListDTO {
    public List<UserReceiptDTO> Items { get; set; } = [];

    /// <summary>Null when the user may upload without limit.</summary>
    public int? MaxCount { get; set; }
    public int MaxFileMegabytes { get; set; }
}

public class UserReceiptRenameRequest {
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
}
