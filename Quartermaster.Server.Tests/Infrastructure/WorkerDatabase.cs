using System;
using System.Threading;
using FluentMigrator.Runner;
using LinqToDB;
using LinqToDB.AspNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.Migrations;

namespace Quartermaster.Server.Tests.Infrastructure;

public sealed class WorkerDatabase {
    private const string ServerConnectionString = "server=localhost;user id=root;";

    public int WorkerId { get; }
    public string DatabaseName { get; }
    public string ConnectionString { get; }

    /// <summary>
    /// Shared <see cref="IntegrationTestFactory"/> for all tests leasing this worker.
    /// Created lazily on first access; disposed when the worker DB is dropped.
    /// </summary>
    private IntegrationTestFactory? _factory;
    private E2ETestFactory? _e2eFactory;
    private readonly Lock _factoryLock = new();

    public IntegrationTestFactory Factory {
        get {
            if (_factory != null)
                return _factory;
            lock (_factoryLock) {
                _factory ??= new IntegrationTestFactory(ConnectionString);
            }
            return _factory;
        }
    }

    /// <summary>
    /// Shared <see cref="E2ETestFactory"/> (real Kestrel host on an ephemeral port)
    /// for all Playwright tests leasing this worker. Created lazily on first access;
    /// disposed when the worker DB is dropped. Previously per-test, which leaked
    /// one host + service provider per Playwright test.
    /// </summary>
    public E2ETestFactory E2EFactory {
        get {
            if (_e2eFactory != null)
                return _e2eFactory;
            lock (_factoryLock) {
                _e2eFactory ??= new E2ETestFactory(ConnectionString);
            }
            return _e2eFactory;
        }
    }

    internal WorkerDatabase(int workerId) {
        WorkerId = workerId;
        DatabaseName = $"quartermaster_test_w{workerId}";
        ConnectionString = $"server=localhost;user id=root;database={DatabaseName};";
        CreateDatabase();
        RunMigrations();
    }

    public DbContext CreateDbContext() {
        var dataOptions = new DataOptions().UseMySqlConnector(ConnectionString);
        return new DbContext(dataOptions);
    }

    public IServiceProvider CreateServiceProvider() {
        var services = new ServiceCollection();
        services.AddLinqToDBContext<DbContext>((provider, options)
            => options.UseMySqlConnector(ConnectionString));
        DbContext.AddRepositories(services);
        return services.BuildServiceProvider();
    }

    public void CleanAllTables() {
        using var conn = new MySqlConnector.MySqlConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SET FOREIGN_KEY_CHECKS = 0;
            TRUNCATE TABLE AgendaItems;
            TRUNCATE TABLE Meetings;
            TRUNCATE TABLE AuditLogs;
            TRUNCATE TABLE NotificationLogs;
            TRUNCATE TABLE UserNotificationPreferences;
            TRUNCATE TABLE TelegramLinkTokens;
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
            TRUNCATE TABLE LoginAttempts;
            TRUNCATE TABLE Users;
            TRUNCATE TABLE Chapters;
            TRUNCATE TABLE AdminDivisionImportLogs;
            TRUNCATE TABLE AdministrativeDivisions;
            TRUNCATE TABLE UserRoleAssignments;
            TRUNCATE TABLE RolePermissions;
            TRUNCATE TABLE Roles;
            TRUNCATE TABLE Permissions;
            SET FOREIGN_KEY_CHECKS = 1;
            """;
        cmd.ExecuteNonQuery();
    }

    internal void Drop() {
        lock (_factoryLock) {
            _factory?.Dispose();
            _factory = null;
            _e2eFactory?.Dispose();
            _e2eFactory = null;
        }
        using var conn = new MySqlConnector.MySqlConnection(ServerConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`;";
        cmd.ExecuteNonQuery();
    }

    private void CreateDatabase() {
        using var conn = new MySqlConnector.MySqlConnection(ServerConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{DatabaseName}`;";
        cmd.ExecuteNonQuery();
    }

    private void RunMigrations() {
        var services = new ServiceCollection();
        services.AddLogging(lb => lb.SetMinimumLevel(LogLevel.Warning));
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
