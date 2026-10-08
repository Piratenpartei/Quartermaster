using System.Collections.Generic;

namespace Quartermaster.Server.Accreditation;

public record NewKeycloakUser(string Username, string FirstName, string LastName, string? Email, string Password,
    Dictionary<string, List<string>> Attributes);
