using InterpolatedSql.Dapper;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data.Abstract;
using System;
using System.Linq;

namespace Quartermaster.Data.Users;

public class UserRepository {
    private readonly DbContext _context;

    public UserRepository(DbContext context) {
        _context = context;
    }

    public void Create(User user) => _context.Insert(user);

    public User? GetById(Guid id) => _context.Users.Where(u => u.Id == id).FirstOrDefault();

    public User? GetByUsername(string username)
        => _context.Users.Where(u => u.Username == username).FirstOrDefault();

    public void SupplementDefaults(RootAccountSettings accountSettings) {
        var admin = GetByUsername(accountSettings.Username);
        if (admin == null)
            admin = AddRootAccount(accountSettings);

        _context.GlobalPermissions.AddForUser(admin.Id, _context.Permissions.GetByIdentifier(PermissionIdentifier.CreateUser)!);
        _context.GlobalPermissions.AddForUser(admin.Id, _context.Permissions.GetByIdentifier(PermissionIdentifier.CreateChapter)!);
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