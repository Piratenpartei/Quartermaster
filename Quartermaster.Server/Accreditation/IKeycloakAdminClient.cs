using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Accreditation;

public interface IKeycloakAdminClient {
    /// <summary>Accounts whose member_number equals <paramref name="memberNumber"/> exactly (trimmed), in any state.</summary>
    Task<List<KeycloakUserRepresentation>> FindByMemberNumberAsync(string memberNumber, CancellationToken ct);

    /// <summary>Case-insensitive exact username check.</summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct);

    /// <summary>Creates an enabled user with a final password; returns the new Keycloak user id.</summary>
    Task<string> CreateUserAsync(NewKeycloakUser user, CancellationToken ct);

    /// <summary>Sets one attribute to a single value, leaving every other attribute untouched.</summary>
    Task SetAttributeAsync(string userId, string name, string value, CancellationToken ct);

    /// <summary>Lists at most one account of the realm, proving the credentials and the user management role.</summary>
    Task CheckAccessAsync(CancellationToken ct);
}
