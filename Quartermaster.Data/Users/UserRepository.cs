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

    public User? GetById(Guid id) => _context.Users.Where(u => u.Id == id).FirstOrDefault();

    public User? GetByUsername(string username)
        => _context.Users.Where(u => u.Username == username).FirstOrDefault();

    public void SupplementDefaults(RootAccountSettings accountSettings) {
        var admin = GetByUsername(accountSettings.Username);
        admin ??= AddRootAccount(accountSettings);

        SupplementDefaultPermission(admin.Id, PermissionIdentifier.CreateUser);
        SupplementDefaultPermission(admin.Id, PermissionIdentifier.CreateChapter);
    }

    private void SupplementDefaultPermission(Guid userId, string identifier) {
        _userGlobalPermissionRepository.AddForUser(userId, _permissionRepository.GetByIdentifier(identifier)!);
    }

    private User AddRootAccount(RootAccountSettings accountSettings) {
        var rootUser = new User() {
            Username = accountSettings.Username,
            PasswordHash = PasswordHashser.Hash(accountSettings.Password)
        };

        Create(rootUser);
        return rootUser;
    }
}