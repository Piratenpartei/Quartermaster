using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;

namespace Quartermaster.Data;

public class SqlContext {
    private readonly string _connectionString;

    //public PermissionRepository Permissions { get; }
    //public UserChapterPermissionRepository ChapterPermissions { get; }
    //public UserGlobalPermissionRepository GlobalPermissions { get; }

    //public TokenRepository Tokens { get; }
    //public UserRepository Users { get; }
    //public AdministrativeDivisionRepository AdministrativeDivisions { get; set; }

    public SqlContext(IOptions<DatabaseSettings> dbSettings, IOptions<RootAccountSettings> accountSettings) {
        _connectionString = dbSettings.Value.ConnectionString;

        //Permissions = new PermissionRepository(this);
        //ChapterPermissions = new UserChapterPermissionRepository(this);
        //GlobalPermissions = new UserGlobalPermissionRepository(this);
        //
        //Tokens = new TokenRepository(this);
        //Users = new UserRepository(this);
        //AdministrativeDivisions = new AdministrativeDivisionRepository(this);
        //
        //AdministrativeDivisions.SupplementDefaults();
        //Permissions.SupplementDefaults();
        //Users.SupplementDefaults(accountSettings.Value);
    }

    //internal IDbConnection GetConnection() => new MySqlConnection(_connectionString);
}