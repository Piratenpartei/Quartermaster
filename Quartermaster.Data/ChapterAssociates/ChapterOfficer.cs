using System;
using LinqToDB.Mapping;
using Quartermaster.Api.ChapterAssociates;

namespace Quartermaster.Data.ChapterAssociates;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ChapterOfficer {
    public const string TableName = "ChapterAssociates";

    [PrimaryKey(Order = 0)]
    public Guid MemberId { get; set; }
    [PrimaryKey(Order = 1)]
    public Guid ChapterId { get; set; }
    [PrimaryKey(Order = 2)]
    public ChapterOfficerType AssociateType { get; set; }
}