using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Receipts;

namespace Quartermaster.Data.Receipts;

/// <summary>A receipt or route image a member keeps for travel and expense forms; the file lives on disk.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class UserReceipt {
    public const string TableName = "UserReceipts";
    public const int LabelMaxLength = 200;
    public const int FileNameMaxLength = 255;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerUserId { get; set; }
    public string Label { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }

    /// <summary>The file's name inside the owner's receipt folder.</summary>
    public string StoredName { get; set; } = "";
    public DateTime UploadedAt { get; set; }

    public UserReceiptDTO ToDto() {
        return new UserReceiptDTO {
            Id = Id,
            Label = Label,
            FileName = FileName,
            ContentType = ContentType,
            SizeBytes = SizeBytes,
            UploadedAt = UploadedAt
        };
    }
}
