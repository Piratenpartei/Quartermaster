using System;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationAdminDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string EMail { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public int Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
