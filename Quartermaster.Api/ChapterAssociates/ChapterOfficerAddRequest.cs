using System;

namespace Quartermaster.Api.ChapterAssociates;

public class ChapterOfficerAddRequest {
    public Guid MemberId { get; set; }
    public Guid ChapterId { get; set; }
    public ChapterOfficerType AssociateType { get; set; }
}
