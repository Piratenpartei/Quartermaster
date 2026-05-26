using System;
using Quartermaster.Data;
using Quartermaster.Data.AuditLog;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Base for repo / service / helper tests that need only a DbContext against the
/// per-worker test database — no in-process HTTP host, no Kestrel. Wipes all tables
/// in the constructor (runs per test under TUnit's default class lifecycle) and
/// disposes the DbContext via <see cref="IDisposable"/>.
///
/// Subclasses can add their own <c>[Before(Test)]</c> for further seeding; the base
/// ctor has already populated <see cref="Db"/> and <see cref="AuditLog"/> by then.
/// </summary>
public abstract class RepositoryTestBase : IDisposable {
    private bool _disposed;

    protected DbContext Db { get; }
    protected AuditLogRepository AuditLog { get; }

    protected RepositoryTestBase() {
        TestDatabaseFixture.CleanAllTables();
        Db = TestDatabaseFixture.CreateDbContext();
        AuditLog = new AuditLogRepository(Db);
    }

    public virtual void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        Db?.Dispose();
        GC.SuppressFinalize(this);
    }
}
