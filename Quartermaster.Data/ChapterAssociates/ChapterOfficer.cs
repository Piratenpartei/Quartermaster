using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.ChapterAssociates;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ChapterOfficer {
    public const string TableName = "ChapterAssociates";

    public Guid UserId { get; set; }
    public Guid ChapterId { get; set; }
    public ChapterOfficerType AssociateType { get; set; }
}

public enum ChapterOfficerType {
    /// <summary>Chair</summary>
    Captain,
    /// <summary>Vice-Chair</summary>
    FirstOfficer,
    Quartermaster,
    Treasurer,
    ViceTreasurer,
    PoliticalDirector,
    /// <summary>Officer without further specification</summary>
    Member
}