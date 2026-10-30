namespace Quartermaster.Api.MembershipApplications;

public class EmailCodeSendResponse {
    public bool CodeRequired { get; set; }
    public int LifetimeMinutes { get; set; }
}
