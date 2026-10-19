using Quartermaster.Api.Accreditation;

namespace Quartermaster.Server.Accreditation.Endpoints;

public static class KeycloakAccountMapper {
    public static AccreditationAccountDTO ToDto(KeycloakUserRepresentation user) {
        return new AccreditationAccountDTO {
            KeycloakUserId = user.Id,
            Username = user.Username,
            FirstName = user.FirstName ?? "",
            LastName = user.LastName ?? "",
            Email = user.Email,
            MemberNumber = user.Attribute("member_number") ?? "",
            Department = user.Attribute(KeycloakUserRepresentation.DepartmentAttribute),
            Enabled = user.Enabled,
            Closed = user.Attribute("closed_time") != null,
            Eligible = user.Attribute("ekklesia_eligible") == "true",
            Verified = user.Attribute("ekklesia_verified") == "true"
        };
    }
}
