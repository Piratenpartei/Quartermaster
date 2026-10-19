using Quartermaster.Server.Accreditation;

namespace Quartermaster.Server.Tests.Accreditation.Fakes;

/// <summary>In-memory Keycloak; static because the web host resolves a new scoped instance per request. Tests call Reset() and run NotInParallel.</summary>
public class FakeKeycloakAdminClient : IKeycloakAdminClient {
    public static List<KeycloakUserRepresentation> Users { get; } = [];
    public static List<NewKeycloakUser> Created { get; } = [];
    public static KeycloakException? FailNext { get; set; }

    /// <summary>When set, CreateUserAsync also adds a second user with the same member number, simulating a concurrent create by another delegate.</summary>
    public static bool RaceOnCreate { get; set; }

    /// <summary>When set, the next FindByMemberNumberAsync call after a successful CreateUserAsync throws once, simulating the post-create re-check failing although the account was created.</summary>
    public static bool FailOnFindAfterCreate { get; set; }

    /// <summary>When set, the next FindByMemberNumberAsync call after a successful SetAttributeAsync throws once, simulating the re-read failing although the update succeeded.</summary>
    public static bool FailOnFindAfterSetAttribute { get; set; }

    /// <summary>Overrides the exception thrown by FailOnFindAfterCreate/FailOnFindAfterSetAttribute; defaults to a KeycloakException when null, so a test can simulate a transport failure (e.g. HttpRequestException) escaping instead.</summary>
    public static Exception? NextFindException { get; set; }

    /// <summary>When set, SetAttributeAsync removes the user after writing the attribute, simulating the account no longer showing up in a re-read (e.g. renamed or merged concurrently).</summary>
    public static bool VanishAfterSetAttribute { get; set; }

    /// <summary>When set, CreateUserAsync still creates the account but then throws a KeycloakException with a null status code, simulating the create succeeding server-side while the response never reached the caller (timeout).</summary>
    public static bool CreateTimesOutButSucceeds { get; set; }

    /// <summary>When set, the next CreateUserAsync call throws this without creating a user, independently of FailNext (which would also fail the preceding existing-account search).</summary>
    public static KeycloakException? FailNextCreate { get; set; }

    private static bool failNextFind;

    public static void Reset() {
        Users.Clear();
        Created.Clear();
        FailNext = null;
        RaceOnCreate = false;
        FailOnFindAfterCreate = false;
        FailOnFindAfterSetAttribute = false;
        NextFindException = null;
        VanishAfterSetAttribute = false;
        CreateTimesOutButSucceeds = false;
        FailNextCreate = null;
        failNextFind = false;
    }

    public static KeycloakUserRepresentation AddUser(string id, string username, string memberNumber, bool enabled = true, string eligible = "false", bool closed = false,
        string firstName = "Vor", string lastName = "Nach", string? email = null, string? department = null) {
        var attributes = new Dictionary<string, List<string>> {
            ["member_number"] = [memberNumber],
            ["ekklesia_eligible"] = [eligible],
            ["ekklesia_verified"] = ["true"]
        };
        if (closed)
            attributes["closed_time"] = ["2026-10-01T02:00:00.000000+00:00"];
        if (department != null)
            attributes["ekklesia_department"] = [department];
        var user = new KeycloakUserRepresentation(id, username, firstName, lastName, email, enabled, attributes);
        Users.Add(user);
        return user;
    }

    public Task<List<KeycloakUserRepresentation>> FindByMemberNumberAsync(string memberNumber, CancellationToken ct) {
        ThrowIfFailing();
        if (failNextFind) {
            failNextFind = false;
            var exception = NextFindException ?? new KeycloakException(500, "find failed after a prior call succeeded");
            NextFindException = null;
            throw exception;
        }
        return Task.FromResult(Users.Where(u => u.Attribute("member_number") == memberNumber.Trim()).ToList());
    }

    public Task<List<KeycloakUserRepresentation>> SearchAsync(string text, int max, CancellationToken ct) {
        ThrowIfFailing();
        var value = text.Trim();
        return Task.FromResult(Users
            .Where(u => new[] { u.Username, u.FirstName, u.LastName, u.Email }.Any(f => f != null && f.Contains(value, StringComparison.OrdinalIgnoreCase)))
            .Take(max)
            .ToList());
    }

    public Task<KeycloakUserRepresentation?> GetUserAsync(string userId, CancellationToken ct) {
        ThrowIfFailing();
        return Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct) {
        ThrowIfFailing();
        return Task.FromResult(Users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<string> CreateUserAsync(NewKeycloakUser user, CancellationToken ct) {
        ThrowIfFailing();
        if (FailNextCreate != null) {
            var failure = FailNextCreate;
            FailNextCreate = null;
            throw failure;
        }
        Created.Add(user);
        var id = Guid.NewGuid().ToString();
        Users.Add(new KeycloakUserRepresentation(id, user.Username, user.FirstName, user.LastName, user.Email, true, user.Attributes));
        if (RaceOnCreate)
            AddUser(Guid.NewGuid().ToString(), user.Username + ".other", user.Attributes["member_number"][0]);
        if (FailOnFindAfterCreate)
            failNextFind = true;
        if (CreateTimesOutButSucceeds) {
            CreateTimesOutButSucceeds = false;
            throw new KeycloakException(null, "Keycloak timed out");
        }
        return Task.FromResult(id);
    }

    public Task SetAttributeAsync(string userId, string name, string value, CancellationToken ct) {
        ThrowIfFailing();
        var user = Users.Single(u => u.Id == userId);
        user.Attributes[name] = [value];
        if (FailOnFindAfterSetAttribute)
            failNextFind = true;
        if (VanishAfterSetAttribute)
            Users.Remove(user);
        return Task.CompletedTask;
    }

    public Task CheckAccessAsync(CancellationToken ct) {
        ThrowIfFailing();
        return Task.CompletedTask;
    }

    private static void ThrowIfFailing() {
        if (FailNext == null)
            return;
        var failure = FailNext;
        FailNext = null;
        throw failure;
    }
}
