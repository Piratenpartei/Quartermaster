using System;

namespace Quartermaster.Data.ChapterAssociates;

public class ChapterAssociate {
    public Guid UserId { get; set; }
    public Guid ChapterId { get; set; }
    public ChapterAssociateType AssociateType { get; set; }
}

public enum ChapterAssociateType {
    Undefined,
    Chair,
    Treasurer,
    Commissioner
}