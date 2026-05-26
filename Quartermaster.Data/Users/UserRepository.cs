using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using System;
using System.Linq;

namespace Quartermaster.Data.Users;

public class UserRepository {
    private readonly DbContext _context;
    private readonly UserGlobalPermissionRepository _userGlobalPermissionRepository;
    private readonly PermissionRepository _permissionRepository;

    public UserRepository(DbContext context, UserGlobalPermissionRepository userGlobalPermissionRepository,
        PermissionRepository permissionRepository) {
        _context = context;
        _userGlobalPermissionRepository = userGlobalPermissionRepository;
        _permissionRepository = permissionRepository;
    }

    public void Create(User user) => _context.Insert(user);

    public User? Get(Guid id)
        => _context.Users.Where(u => u.Id == id && u.DeletedAt == null).FirstOrDefault();

    public User? GetByUsername(string username)
        => _context.Users.Where(u => u.Username == username && u.DeletedAt == null).FirstOrDefault();

    public User? GetByEmail(string email)
        => _context.Users.Where(u => u.Email == email && u.DeletedAt == null).FirstOrDefault();

    public void SupplementDefaults(
        RootAccountSettings? accountSettings,
        ChapterRepository chapterRepo,
        UserChapterPermissionRepository chapterPermRepo) {

        if (accountSettings == null || string.IsNullOrEmpty(accountSettings.Username) || string.IsNullOrEmpty(accountSettings.Password))
            return;

        var admin = GetByUsername(accountSettings.Username);
        admin ??= AddRootAccount(accountSettings);

        // Grant all global permissions
        var allPermissions = _permissionRepository.GetAll();
        foreach (var perm in allPermissions.Where(p => p.Global))
            _userGlobalPermissionRepository.AddForUser(admin.Id, perm);

        // Grant all chapter-scoped permissions for the federal chapter (Bundesverband = root chapter)
        var roots = chapterRepo.GetRoots();
        var bundesverband = roots.FirstOrDefault();
        if (bundesverband != null) {
            foreach (var perm in allPermissions.Where(p => !p.Global))
                chapterPermRepo.AddForUser(admin.Id, bundesverband.Id, perm.Id);
        }
    }

    /// <summary>
    /// Creates the dev/root admin user, leaving most NOT NULL profile fields at default.
    /// Two FK columns (<c>CitizenshipAdministrativeDivisionId</c>, <c>AddressAdministrativeDivisionId</c>)
    /// default to <see cref="Guid.Empty"/> and require the "Null Island" <c>AdministrativeDivision</c>
    /// row seeded by <see cref="AdministrativeDivisionRepository.SupplementDefaults"/>. Throws if it's
    /// missing rather than letting the insert fail with an opaque FK violation later.
    /// </summary>
    private User AddRootAccount(RootAccountSettings accountSettings) {
        var nullIsland = _context.AdministrativeDivisions
            .Where(d => d.Id == Guid.Empty)
            .FirstOrDefault();
        if (nullIsland == null) {
            throw new InvalidOperationException(
                "Cannot create root account: Null Island AdministrativeDivision (Id=Guid.Empty) is not seeded. " +
                "Ensure AdministrativeDivisionRepository.SupplementDefaults runs before UserRepository.SupplementDefaults.");
        }

        var rootUser = new User() {
            Username = accountSettings.Username!,
            PasswordHash = PasswordHasher.Hash(accountSettings.Password!)
        };

        Create(rootUser);
        return rootUser;
    }

    public void UpdateEmail(Guid id, string email) {
        _context.Users
            .Where(u => u.Id == id)
            .Set(u => u.Email, email)
            .Update();
    }

    public void SoftDelete(Guid id) {
        _context.Users.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
    }
}