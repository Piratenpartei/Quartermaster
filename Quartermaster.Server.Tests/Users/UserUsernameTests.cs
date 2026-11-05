using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Users;

/// <summary>The username always comes from the SSO; an account that still holds it after a rename in Keycloak keeps it until it changes.</summary>
public class UserUsernameTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;
    private UserRepository _users = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _users = new UserRepository(Db, new UserGlobalPermissionRepository(Db, new RoleRepository(Db)), new PermissionRepository(Db));
    }

    private string? UsernameOf(Guid userId) {
        return Db.Users.Single(u => u.Id == userId).Username;
    }

    [Test]
    public async Task A_free_username_replaces_the_email() {
        var user = _builder.SeedUser(username: "anna@example.org");

        var outcome = _users.SetUsernameFromSso(user.Id, "anna.pirat");

        await Assert.That(outcome).IsEqualTo(UsernameUpdate.Set);
        await Assert.That(UsernameOf(user.Id)).IsEqualTo("anna.pirat");
    }

    [Test]
    public async Task The_same_username_is_left_alone() {
        var user = _builder.SeedUser(username: "anna.pirat");

        await Assert.That(_users.SetUsernameFromSso(user.Id, "anna.pirat")).IsEqualTo(UsernameUpdate.Unchanged);
    }

    [Test]
    public async Task A_username_another_active_account_holds_is_not_taken() {
        var holder = _builder.SeedUser(username: "anna.pirat");
        var user = _builder.SeedUser(username: "anna@example.org");

        var outcome = _users.SetUsernameFromSso(user.Id, "anna.pirat");

        await Assert.That(outcome).IsEqualTo(UsernameUpdate.Taken);
        await Assert.That(UsernameOf(user.Id)).IsEqualTo("anna@example.org");
        await Assert.That(UsernameOf(holder.Id)).IsEqualTo("anna.pirat");
    }

    [Test]
    public async Task A_username_only_a_deleted_account_holds_moves_over() {
        var deleted = _builder.SeedUser(username: "anna.pirat");
        Db.Users.Where(u => u.Id == deleted.Id).Set(u => u.DeletedAt, DateTime.UtcNow).Update();
        var user = _builder.SeedUser(username: "anna@example.org");

        var outcome = _users.SetUsernameFromSso(user.Id, "anna.pirat");

        await Assert.That(outcome).IsEqualTo(UsernameUpdate.Set);
        await Assert.That(UsernameOf(user.Id)).IsEqualTo("anna.pirat");
        await Assert.That(UsernameOf(deleted.Id)).IsNull();
    }

    [Test]
    public async Task Usernames_up_to_the_keycloak_limit_fit_and_longer_ones_are_refused() {
        var user = _builder.SeedUser(username: "anna@example.org");
        var longest = new string('a', User.UsernameMaxLength);

        var fits = _users.SetUsernameFromSso(user.Id, longest);
        var tooLong = _users.SetUsernameFromSso(user.Id, longest + "a");

        await Assert.That(fits).IsEqualTo(UsernameUpdate.Set);
        await Assert.That(tooLong).IsEqualTo(UsernameUpdate.TooLong);
        await Assert.That(UsernameOf(user.Id)).IsEqualTo(longest);
    }
}
