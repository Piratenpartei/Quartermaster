using System;

namespace Quartermaster.Api.MembershipApplications;

public class EmailCodeSendRequest {
    public string Email { get; set; } = "";
    public Guid? ChapterId { get; set; }
}
