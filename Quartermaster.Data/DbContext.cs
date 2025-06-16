using System;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;

namespace Quartermaster.Data;

public class DbContext : DataConnection {
    public ITable<Token> Tokens => this.GetTable<Token>();
    public ITable<Permission> Permissions => this.GetTable<Permission>();
    public ITable<UserChapterPermission> UserChapterPermissions => this.GetTable<UserChapterPermission>();
    public ITable<UserGlobalPermission> UserGlobalPermissions => this.GetTable<UserGlobalPermission>();
    public ITable<AdministrativeDivision> AdministrativeDivisions => this.GetTable<AdministrativeDivision>();
    public ITable<User> Users => this.GetTable<User>();
    public ITable<DueSelection> DueSelections => this.GetTable<DueSelection>();

    public DbContext(DataOptions dataOptions) : base(dataOptions) { }

    public static void AddRepositories(IServiceCollection services) {
        services.AddScoped<TokenRepository>();
        services.AddScoped<PermissionRepository>();
        services.AddScoped<UserChapterPermissionRepository>();
        services.AddScoped<UserGlobalPermissionRepository>();
        services.AddScoped<AdministrativeDivisionRepository>();
        services.AddScoped<UserRepository>();
        services.AddScoped<DueSelectionRepository>();
    }

    public static void SupplementDefaults(IServiceProvider services) {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>().SupplementDefaults();
        scope.ServiceProvider.GetRequiredService<PermissionRepository>().SupplementDefaults();
        scope.ServiceProvider.GetRequiredService<UserRepository>().SupplementDefaults(
            services.GetRequiredService<IOptions<RootAccountSettings>>().Value);
    }
}