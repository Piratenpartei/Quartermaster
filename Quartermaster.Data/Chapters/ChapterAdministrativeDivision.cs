using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Chapters;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ChapterAdministrativeDivision {
    public const string TableName = "ChapterAdministrativeDivisions";

    [PrimaryKey(Order = 0)]
    public Guid ChapterId { get; set; }
    [PrimaryKey(Order = 1)]
    public Guid AdministrativeDivisionId { get; set; }
}
