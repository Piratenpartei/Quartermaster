using InterpolatedSql.Dapper;
using Quartermaster.Api;
using Quartermaster.Data.Abstract;
using System;

namespace Quartermaster.Data.Users;

public class UserRepository : RepositoryBase<User> {
    private readonly DbContext _context;

    public UserRepository(DbContext context) {
        _context = context;
    }

    public User Create(User user) {
        EnsureSetGuid(user, u => u.Id);

        using var con = _context.GetConnection();
        con.SqlBuilder($"INSERT INTO Users (Id, Username, EMail, PasswordHash, FirstName," +
            $"LastName, DateOfBirth, CitizenshipAdministrativeDivisionId, PhoneNumber," +
            $"MembershipFee, MemberSince, AddressStreet, AddressHouseNbr, AddressAdministrativeDivisionId," +
            $"ChapterId) VALUES ({user.Id}, {user.Username}, {user.EMail}, {user.PasswordHash}," +
            $"{user.FirstName}, {user.LastName}, {user.DateOfBirth}, {user.CitizenshipAdministrativeDivisionId}," +
            $"{user.PhoneNumber}, {user.MembershipFee}, {user.MemberSince}, {user.AddressStreet}," +
            $"{user.AddressHouseNbr}, {user.AddressAdministrativeDivisionId}, {user.ChapterId})")
            .Execute();

        return user;
    }

    public User? GetById(Guid id) {
        using var con = _context.GetConnection();
        return con.SqlBuilder($"SELECT * FROM Users WHERE Id = {id}").QuerySingleOrDefault<User>();
    }

    public User? GetByUsername(string username) {
        using var con = _context.GetConnection();
        return con.SqlBuilder($"SELECT * FROM Users WHERE UserName = {username}").QuerySingleOrDefault<User>();
    }

    public void SupplementDefaults(RootAccountSettings accountSettings) {
        var admin = GetByUsername(accountSettings.Username);
        if (admin == null)
            admin = AddRootAccount(accountSettings);

        _context.GlobalPermissions.AddForUser(admin.Id, _context.Permissions.GetByIdentifier(PermissionIdentifier.CreateUser)!);
        _context.GlobalPermissions.AddForUser(admin.Id, _context.Permissions.GetByIdentifier(PermissionIdentifier.CreateChapter)!);
    }

    private User AddRootAccount(RootAccountSettings accountSettings) {
        return Create(new User() {
            Username = accountSettings.Username,
            PasswordHash = PasswordHashser.Hash(accountSettings.Password)
        });
    }
}