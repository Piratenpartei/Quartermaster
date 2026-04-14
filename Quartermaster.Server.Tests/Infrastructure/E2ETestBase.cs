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
    private readonly System.Collections.Generic.List<IBrowserContext> _extraContexts = new();
    private bool _disposed;

    protected E2ETestFactory Factory { get; private set; } = default!;
    protected IPage Page { get; private set; } = default!;
    protected DbContext Db { get; private set; } = default!;
    protected TestDataBuilder Builder { get; private set; } = default!;
    protected WorkerDatabase Database { get; private set; } = default!;
    protected string BaseUrl => Factory.BaseUrl;

    /// <summary>
    /// Creates an additional browser context + page with the given auth token
    /// injected into localStorage before navigation. Useful for tests that need
    /// multiple authenticated users (e.g., collaborative editing) because
    /// localStorage is per-origin-per-context — the default <see cref="Page"/>
    /// alone can only hold one user's token at a time. All extra contexts are
    /// cleaned up automatically in teardown.
    /// </summary>
    protected async Task<IPage> NewAuthenticatedPageAsync(string authToken) {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl
        });
        _extraContexts.Add(context);
        await context.AddInitScriptAsync(
            $"window.localStorage.setItem('auth_token', '{authToken}');");
        return await context.NewPageAsync();
    }

    /// <summary>
    /// Creates an additional browser context + page with no auth token at all —
    /// used to simulate an anonymous visitor. Blazor's AuthService treats
    /// missing localStorage entries as "not logged in" rather than rejecting.
    /// </summary>
    protected async Task<IPage> NewAnonymousPageAsync() {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl
        });
        _extraContexts.Add(context);
        return await context.NewPageAsync();
    }

    /// <summary>
    /// Injects the given auth token into the default <see cref="Page"/>'s
    /// localStorage. Must be called before the first navigation in the test
    /// because Blazor reads the token exactly once at boot.
    /// </summary>
    protected async Task InjectAuthTokenAsync(string authToken) {
        await Page.AddInitScriptAsync(
            $"window.localStorage.setItem('auth_token', '{authToken}');");
    }

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
        foreach (var ctx in _extraContexts) {
            try { await ctx.CloseAsync(); } catch { }
        }
        _extraContexts.Clear();
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
