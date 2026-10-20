using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Server.Accreditation;

public record KeycloakUserRepresentation(string Id, string Username, string? FirstName, string? LastName, string? Email, bool Enabled,
    Dictionary<string, List<string>> Attributes) {
    public const string DepartmentAttribute = "ekklesia_department";
    public const string EligibleAttribute = "ekklesia_eligible";

    public string? Attribute(string name) => Attributes.TryGetValue(name, out var values) ? values.FirstOrDefault()?.Trim() : null;
}
