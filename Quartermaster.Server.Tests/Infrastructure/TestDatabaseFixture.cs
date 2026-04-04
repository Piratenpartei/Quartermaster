using System;
using FluentMigrator.Runner;
using LinqToDB;
using LinqToDB.AspNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.Migrations;

namespace Quartermaster.Server.Tests.Infrastructure;

public static class TestDatabaseFixture {
    private static readonly string ConnectionString =
        "server=localhost;user id=root;database=quartermaster_test;";

    private static bool _initialized;
    private static readonly object _lock = new();

    public static void EnsureInitialized() {
        if (_initialized)
            return;

        lock (_lock) {
            if (_initialized)
                return;

            CreateDatabase();
            RunMigrations();
            _initialized = true;
        }
    }

    public static DbContext CreateDbContext() {
        EnsureInitialized();
        var dataOptions = new DataOptions().UseMySqlConnector(ConnectionString);
        return new DbContext(dataOptions);
    }

    public static IServiceProvider CreateServiceProvider() {
        EnsureInitialized();
        var services = new ServiceCollection();
        services.AddLinqToDBContext<DbContext>((provider, options)
            => options.UseMySqlConnector(ConnectionString));
        DbContext.AddRepositories(services);
        return services.BuildServiceProvider();
    }

    public static void CleanAllTables() {
        EnsureInitialized();
        using var conn = new MySqlConnector.MySqlConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SET FOREIGN_KEY_CHECKS = 0;
            TRUNCATE TABLE AuditLogs;
            TRUNCATE TABLE EmailLogs;
            TRUNCATE TABLE EventChecklistItems;
            TRUNCATE TABLE Events;
            TRUNCATE TABLE EventTemplates;
            TRUNCATE TABLE MemberImportLogs;
            TRUNCATE TABLE Members;
            TRUNCATE TABLE MembershipApplications;
            TRUNCATE TABLE DueSelections;
            TRUNCATE TABLE SystemOptions;
            TRUNCATE TABLE OptionDefinitions;
            TRUNCATE TABLE MotionVotes;
            TRUNCATE TABLE Motions;
            TRUNCATE TABLE ChapterAssociates;
            TRUNCATE TABLE UserChapterPermissions;
            TRUNCATE TABLE UserGlobalPermissions;
            TRUNCATE TABLE Tokens;
            TRUNCATE TABLE Users;
            TRUNCATE TABLE Chapters;
            TRUNCATE TABLE AdminDivisionImportLogs;
            TRUNCATE TABLE AdministrativeDivisions;
            TRUNCATE TABLE Permissions;
            SET FOREIGN_KEY_CHECKS = 1;
            """;
        cmd.ExecuteNonQuery();
    }

    private static void CreateDatabase() {
        using var conn = new MySqlConnector.MySqlConnection("server=localhost;user id=root;");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE DATABASE IF NOT EXISTS quartermaster_test;";
        cmd.ExecuteNonQuery();
    }

    private static void RunMigrations() {
        var services = new ServiceCollection();
        services.AddLogging(lb => lb.AddConsole());
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => {
                rb.AddMySql8()
                    .WithGlobalConnectionString(ConnectionString)
                    .ScanIn(typeof(M001_InitialStructureMigration).Assembly).For.Migrations();
            });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
}
