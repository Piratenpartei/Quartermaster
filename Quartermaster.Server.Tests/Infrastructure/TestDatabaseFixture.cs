using System;
using System.Collections.Concurrent;
using System.Threading;
using LinqToDB;
using Quartermaster.Data;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Per-worker MySQL database fixture. Workers are leased per test instance from a bounded
/// pool (<c>quartermaster_test_w{N}</c>), allowing tests to run in parallel without conflicts.
/// Each worker owns ONE <see cref="IntegrationTestFactory"/> shared across all tests that
/// lease that worker — avoids ~500 factory boots per test run (now only ~8).
/// </summary>
public static class TestDatabaseFixture {
    private const int PoolSize = 8;
    private static readonly SemaphoreSlim _poolSemaphore = new(PoolSize, PoolSize);
    private static readonly ConcurrentQueue<int> _availableIds = new();
    private static int _workerCounter;

    private static readonly ThreadLocal<int> _workerId =
        new(() => Interlocked.Increment(ref _workerCounter) - 1);

    private static readonly ConcurrentDictionary<int, Lazy<WorkerDatabase>> _byWorker = new();

    private static WorkerDatabase GetOrCreate(int id) {
        return _byWorker.GetOrAdd(id,
            wid => new Lazy<WorkerDatabase>(() => new WorkerDatabase(wid), LazyThreadSafetyMode.ExecutionAndPublication)
        ).Value;
    }

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

    public static void DropAllWorkerDatabases() {
        foreach (var lazy in _byWorker.Values) {
            if (lazy.IsValueCreated)
                lazy.Value.Drop();
        }
        _byWorker.Clear();
    }
}
