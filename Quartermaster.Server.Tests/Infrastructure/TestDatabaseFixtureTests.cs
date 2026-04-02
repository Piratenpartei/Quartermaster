namespace Quartermaster.Server.Tests.Infrastructure;

public class TestDatabaseFixtureTests {
    [Test]
    public async Task DatabaseInitializes() {
        TestDatabaseFixture.EnsureInitialized();
        using var ctx = TestDatabaseFixture.CreateDbContext();
        var count = ctx.Users.Count();
        await Assert.That(count).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task CleanTablesWorks() {
        TestDatabaseFixture.CleanAllTables();
        using var ctx = TestDatabaseFixture.CreateDbContext();
        await Assert.That(ctx.Users.Count()).IsEqualTo(0);
    }
}
