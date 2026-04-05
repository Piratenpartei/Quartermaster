using System;
using System.Collections.Concurrent;
using System.Threading;
using FluentMigrator.Runner;
using LinqToDB;
using LinqToDB.AspNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.Migrations;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Per-worker MySQL database fixture. Workers are leased per test instance from a bounded
/// pool (<c>quartermaster_test_w{N}</c>), allowing tests to run in parallel without conflicts.
/// Databases are created + migrated lazily and reused for the lifetime of the process.
/// <para>
/// A test calls <see cref="Acquire"/> in its constructor and <see cref="Release"/> on dispose;
/// leases are pinned to the test instance and therefore survive async thread-hops.
/// </para>
/// </summary>
public static class TestDatabaseFixture {
    private const string ServerConnectionString = "server=localhost;user id=root;";

    // Pool size caps max concurrent DBs in use. Matches a reasonable parallelism for
    // integration tests without exhausting MySQL connections or file handles.
    private const int PoolSize = 8;
    private static readonly SemaphoreSlim _poolSemaphore = new(PoolSize, PoolSize);
    private static readonly ConcurrentQueue<int> _availableIds = new();
    private static int _workerCounter;

    private static readonly ThreadLocal<int> _workerId =
        new(() => Interlocked.Increment(ref _workerCounter) - 1);

    // Wrap WorkerDatabase in Lazy<T> so that ConcurrentDictionary.GetOrAdd's value-factory
    // (which CAN be called by multiple threads concurrently for the same key) doesn't end up
    // running schema migrations twice in parallel on the same DB — which races when both
    // threads try to CREATE TABLE simultaneously.
    private static readonly ConcurrentDictionary<int, Lazy<WorkerDatabase>> _byWorker = new();

    private static WorkerDatabase GetOrCreate(int id) {
        return _byWorker.GetOrAdd(id,
            wid => new Lazy<WorkerDatabase>(() => new WorkerDatabase(wid), LazyThreadSafetyMode.ExecutionAndPublication)
        ).Value;
    }

    /// <summary>
    /// Leases a worker database for the calling test. Blocks if the pool is exhausted.
    /// Callers MUST invoke <see cref="Release"/> with the returned id when the test ends.
    /// </summary>
    public static WorkerDatabase Acquire() {
        _poolSemaphore.Wait();
        if (!_availableIds.TryDequeue(out var id))
            id = Interlocked.Increment(ref _workerCounter) - 1;
        return GetOrCreate(id);
    }

    public static void Release(WorkerDatabase db) {
        _availableIds.Enqueue(db.WorkerId);
        _poolSemaphore.Release();
    }

    public static WorkerDatabase ForCurrentWorker() {
        var id = _workerId.Value;
        return GetOrCreate(id);
    }

    public static void EnsureInitialized() {
        _ = ForCurrentWorker();
    }

    public static string ConnectionString => ForCurrentWorker().ConnectionString;

    public static DbContext CreateDbContext()
        => ForCurrentWorker().CreateDbContext();

    public static IServiceProvider CreateServiceProvider()
        => ForCurrentWorker().CreateServiceProvider();

    public static void CleanAllTables()
        => ForCurrentWorker().CleanAllTables();

    /// <summary>
    /// Drops all worker databases. Called from assembly-level teardown unless
    /// QM_TEST_KEEP_DB=1 is set in the environment.
    /// </summary>
    public static void DropAllWorkerDatabases() {
        foreach (var lazy in _byWorker.Values) {
            if (lazy.IsValueCreated)
                lazy.Value.Drop();
        }
        _byWorker.Clear();
    }
}

public sealed class WorkerDatabase {
    private const string ServerConnectionString = "server=localhost;user id=root;";

    public int WorkerId { get; }
    public string DatabaseName { get; }
    public string ConnectionString { get; }

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
