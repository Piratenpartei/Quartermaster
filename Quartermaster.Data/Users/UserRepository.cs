using InterpolatedSql.Dapper;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data.Abstract;
using Quartermaster.Data.Permissions;
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

    public User? GetById(Guid id)
        => _context.Users.Where(u => u.Id == id && u.DeletedAt == null).FirstOrDefault();

    public User? GetByUsername(string username)
        => _context.Users.Where(u => u.Username == username && u.DeletedAt == null).FirstOrDefault();

    public User? GetByEmail(string email)
        => _context.Users.Where(u => u.EMail == email && u.DeletedAt == null).FirstOrDefault();

    public void SupplementDefaults(RootAccountSettings? accountSettings) {
        if (accountSettings == null || string.IsNullOrEmpty(accountSettings.Username) || string.IsNullOrEmpty(accountSettings.Password))
            return;

        var admin = GetByUsername(accountSettings.Username);
        admin ??= AddRootAccount(accountSettings);

        SupplementDefaultPermission(admin.Id, PermissionIdentifier.CreateUser);
        SupplementDefaultPermission(admin.Id, PermissionIdentifier.CreateChapter);
        SupplementDefaultPermission(admin.Id, PermissionIdentifier.ViewOptions);
        SupplementDefaultPermission(admin.Id, PermissionIdentifier.EditOptions);
        SupplementDefaultPermission(admin.Id, PermissionIdentifier.ViewUsers);
        SupplementDefaultPermission(admin.Id, PermissionIdentifier.ViewAudit);
        SupplementDefaultPermission(admin.Id, PermissionIdentifier.ViewEmailLogs);
        SupplementDefaultPermission(admin.Id, PermissionIdentifier.TriggerMemberImport);
    }

    private void SupplementDefaultPermission(Guid userId, string identifier) {
        _userGlobalPermissionRepository.AddForUser(userId, _permissionRepository.GetByIdentifier(identifier)!);
    }

    private User AddRootAccount(RootAccountSettings accountSettings) {
        var rootUser = new User() {
            Username = accountSettings.Username!,
            PasswordHash = PasswordHashser.Hash(accountSettings.Password!)
        };

        Create(rootUser);
        return rootUser;
    }

    public void UpdateEmail(Guid id, string email) {
        _context.Users
            .Where(u => u.Id == id)
            .Set(u => u.EMail, email)
            .Update();
    }

    public void SoftDelete(Guid id) {
        _context.Users.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
    }
}