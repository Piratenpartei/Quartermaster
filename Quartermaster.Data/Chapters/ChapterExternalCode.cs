using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Chapters;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ChapterExternalCode {
    public const string TableName = "ChapterExternalCodes";

    [PrimaryKey(Order = 0)]
    public Guid ChapterId { get; set; }
    [PrimaryKey(Order = 1)]
    public string Code { get; set; } = "";
}
