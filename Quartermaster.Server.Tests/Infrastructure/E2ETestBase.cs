using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Quartermaster.Data;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Base class for browser-driven E2E tests. Boots a real Kestrel server (via
/// <see cref="E2ETestFactory"/>) on an ephemeral port, launches a headless Chromium
/// via Playwright, and provides a configured <see cref="IPage"/> to tests.
/// Tests seed data via <see cref="TestDataBuilder"/> before driving the UI.
/// </summary>
public abstract class E2ETestBase : IDisposable {
    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;
    private IBrowserContext _context = default!;
    private bool _disposed;

    protected E2ETestFactory Factory { get; private set; } = default!;
    protected IPage Page { get; private set; } = default!;
    protected DbContext Db { get; private set; } = default!;
    protected TestDataBuilder Builder { get; private set; } = default!;
    protected WorkerDatabase Database { get; private set; } = default!;
    protected string BaseUrl => Factory.BaseUrl;

    [Before(Test)]
    public async Task SetupBrowser() {
        TestDatabaseFixture.CleanAllTables();
        Database = TestDatabaseFixture.ForCurrentWorker();
        Db = Database.CreateDbContext();
        Builder = new TestDataBuilder(Db);
        Factory = new E2ETestFactory(Database.ConnectionString);

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions {
            Headless = true
        });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl
        });
        Page = await _context.NewPageAsync();
    }

    [After(Test)]
    public async Task TeardownBrowser() {
        if (_context != null) await _context.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    public virtual void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        Factory?.Dispose();
        Db?.Dispose();
        GC.SuppressFinalize(this);
    }
}
