using System;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Options;
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
    public ITable<Chapter> Chapters => this.GetTable<Chapter>();
    public ITable<MembershipApplication> MembershipApplications => this.GetTable<MembershipApplication>();
    public ITable<ChapterOfficer> ChapterOfficers => this.GetTable<ChapterOfficer>();
    public ITable<Motion> Motions => this.GetTable<Motion>();
    public ITable<MotionVote> MotionVotes => this.GetTable<MotionVote>();
    public ITable<SystemOption> SystemOptions => this.GetTable<SystemOption>();
    public ITable<OptionDefinition> OptionDefinitions => this.GetTable<OptionDefinition>();
    public ITable<Member> Members => this.GetTable<Member>();
    public ITable<MemberImportLog> MemberImportLogs => this.GetTable<MemberImportLog>();
    public ITable<Event> Events => this.GetTable<Event>();
    public ITable<EventChecklistItem> EventChecklistItems => this.GetTable<EventChecklistItem>();
    public ITable<EventTemplate> EventTemplates => this.GetTable<EventTemplate>();

    public DbContext(DataOptions dataOptions) : base(dataOptions) { }

    public static void AddRepositories(IServiceCollection services) {
        services.AddScoped<TokenRepository>();
        services.AddScoped<PermissionRepository>();
        services.AddScoped<UserChapterPermissionRepository>();
        services.AddScoped<UserGlobalPermissionRepository>();
        services.AddScoped<AdministrativeDivisionRepository>();
        services.AddScoped<UserRepository>();
        services.AddScoped<DueSelectionRepository>();
        services.AddScoped<ChapterRepository>();
        services.AddScoped<MembershipApplicationRepository>();
        services.AddScoped<ChapterOfficerRepository>();
        services.AddScoped<MotionRepository>();
        services.AddScoped<OptionRepository>();
        services.AddScoped<MemberRepository>();
        services.AddScoped<EventRepository>();
    }

    public static void SupplementDefaults(IServiceProvider services) {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>().SupplementDefaults(true);
        scope.ServiceProvider.GetRequiredService<ChapterRepository>().SupplementDefaults(
            scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>());
        scope.ServiceProvider.GetRequiredService<PermissionRepository>().SupplementDefaults();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SupplementDefaults();
#if DEBUG
        var rootSettings = services.GetRequiredService<IOptions<RootAccountSettings>>().Value;
        if (!string.IsNullOrEmpty(rootSettings.Username) && !string.IsNullOrEmpty(rootSettings.Password)) {
            scope.ServiceProvider.GetRequiredService<UserRepository>().SupplementDefaults(rootSettings);
        }
#endif
        scope.ServiceProvider.GetRequiredService<ChapterOfficerRepository>().SupplementDefaults(
            scope.ServiceProvider.GetRequiredService<ChapterRepository>());
    }
}