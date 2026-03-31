using System;

namespace Quartermaster.Api.ChapterAssociates;

public class ChapterOfficerDTO {
    public Guid MemberId { get; set; }
    public int MemberNumber { get; set; }
    public string MemberFirstName { get; set; } = "";
    public string MemberLastName { get; set; } = "";
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public int AssociateType { get; set; }
}
