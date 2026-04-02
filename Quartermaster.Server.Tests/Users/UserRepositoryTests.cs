using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Users;

[NotInParallel]
public class UserRepositoryTests : IDisposable {
    private DbContext _context = default!;
    private UserRepository _userRepo = default!;
    private PermissionRepository _permissionRepo = default!;
    private UserGlobalPermissionRepository _userGlobalPermissionRepo = default!;

    [Before(Test)]
    public void Setup() {
        TestDatabaseFixture.CleanAllTables();
        _context = TestDatabaseFixture.CreateDbContext();
        _permissionRepo = new PermissionRepository(_context);
        _userGlobalPermissionRepo = new UserGlobalPermissionRepository(_context);
        _userRepo = new UserRepository(_context, _userGlobalPermissionRepo, _permissionRepo);

        // Seed an AdministrativeDivision with Guid.Empty for User FK defaults
        _context.Insert(new AdministrativeDivision {
            Id = Guid.Empty,
            Name = "Default Division",
            Depth = 0
        });

        // Seed permissions that SupplementDefaults expects
        _permissionRepo.SupplementDefaults();
    }

    // --- SupplementDefaults ---

    [Test]
    public async Task SupplementDefaults_NullSettings_NoUserCreated() {
        _userRepo.SupplementDefaults(null);

        var users = _context.Users.ToList();
        await Assert.That(users.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SupplementDefaults_EmptyUsername_NoUserCreated() {
        _userRepo.SupplementDefaults(new RootAccountSettings {
            Username = "",
            Password = "secret"
        });

        var users = _context.Users.ToList();
        await Assert.That(users.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SupplementDefaults_NewUser_CreatedWithCorrectUsername() {
        _userRepo.SupplementDefaults(new RootAccountSettings {
            Username = "admin",
            Password = "secret123"
        });

        var user = _userRepo.GetByUsername("admin");
        await Assert.That(user).IsNotNull();
        await Assert.That(user!.Username).IsEqualTo("admin");
        await Assert.That(user.PasswordHash).IsNotNull();
    }

    [Test]
    public async Task SupplementDefaults_ExistingUser_NotDuplicated() {
        _userRepo.SupplementDefaults(new RootAccountSettings {
            Username = "admin",
            Password = "secret123"
        });
        _userRepo.SupplementDefaults(new RootAccountSettings {
            Username = "admin",
            Password = "secret123"
        });

        var users = _context.Users.Where(u => u.Username == "admin" && u.DeletedAt == null).ToList();
        await Assert.That(users.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SupplementDefaults_PermissionsGranted() {
        _userRepo.SupplementDefaults(new RootAccountSettings {
            Username = "admin",
            Password = "secret123"
        });

        var user = _userRepo.GetByUsername("admin")!;
        var permissions = _userGlobalPermissionRepo.GetForUser(user.Id);
        var identifiers = permissions.Select(p => p.Identifier).ToList();

        await Assert.That(identifiers).Contains(PermissionIdentifier.CreateUser);
        await Assert.That(identifiers).Contains(PermissionIdentifier.CreateChapter);
    }

    public void Dispose() {
        _context?.Dispose();
    }
}
