using System;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationAdminDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public ApplicationStatus Status { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
