using System;

namespace Quartermaster.Data.ChapterAssociates;

public class ChapterOfficer {
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