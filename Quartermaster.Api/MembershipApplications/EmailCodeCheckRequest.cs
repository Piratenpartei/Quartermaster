namespace Quartermaster.Api.MembershipApplications;

public class EmailCodeCheckRequest {
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
}
