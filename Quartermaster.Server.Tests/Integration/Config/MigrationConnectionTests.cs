using System.Threading.Tasks;
using FluentMigrator.Runner.Initialization;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Config;

public class MigrationConnectionTests : IntegrationTestBase {
    /// <summary>The host's startup migration must run against the worker database, never the developer's own database.</summary>
    [Test]
    public async Task Startup_migrations_use_the_test_database() {
        using var scope = Factory.Services.CreateScope();

        var connectionString = scope.ServiceProvider.GetRequiredService<IConnectionStringAccessor>().ConnectionString;

        await Assert.That(connectionString).Contains($"database={Database.DatabaseName};");
    }
}
