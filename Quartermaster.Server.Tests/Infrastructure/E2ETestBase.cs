using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Quartermaster.Data;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;

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
    private readonly List<IBrowserContext> _extraContexts = new();
    private bool _disposed;

    protected E2ETestFactory Factory { get; private set; } = default!;
    protected IPage Page { get; private set; } = default!;
    protected DbContext Db { get; private set; } = default!;
    protected TestDataBuilder Builder { get; private set; } = default!;
    protected WorkerDatabase Database { get; private set; } = default!;
    protected string BaseUrl => Factory.BaseUrl;

    /// <summary>
    /// Creates an additional authenticated browser context + page. The auth cookie is set
    /// before navigation; the page hits the app already-logged-in. Cleaned up in teardown.
    /// </summary>
    protected async Task<IPage> NewAuthenticatedPageAsync(string authToken) {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl
        });
        _extraContexts.Add(context);
        await SetAuthCookieAsync(context, authToken);
        return await context.NewPageAsync();
    }

    /// <summary>Creates an additional browser context + page with no auth cookie.</summary>
    protected async Task<IPage> NewAnonymousPageAsync() {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl
        });
        _extraContexts.Add(context);
        return await context.NewPageAsync();
    }

    /// <summary>Sets the auth cookie on the default <see cref="Page"/>'s context. Call before the first navigation.</summary>
    protected async Task InjectAuthTokenAsync(string authToken) {
        await SetAuthCookieAsync(Page.Context, authToken);
    }

    private async Task SetAuthCookieAsync(IBrowserContext context, string authToken) {
        var uri = new Uri(BaseUrl);
        await context.AddCookiesAsync(new[] {
            new Cookie {
                Name = ".Quartermaster.Auth",
                Value = authToken,
                Domain = uri.Host,
                Path = "/",
                HttpOnly = true,
                Secure = uri.Scheme == "https",
                SameSite = SameSiteAttribute.Strict
            }
        });
    }

    [Before(Test)]
    public async Task SetupBrowser() {
        TestDatabaseFixture.CleanAllTables();
        Database = TestDatabaseFixture.ForCurrentWorker();
        // Shared per-worker — disposed by the worker fixture when the DB is dropped,
        // not per-test. Previously a fresh factory + Kestrel host per test, which
        // leaked a service provider + listener socket every time.
        Factory = Database.E2EFactory;

        // CleanAllTables wiped the permission/role/option seed; re-seed via the
        // factory's DI scope so any endpoint hit during the test sees the standard
        // defaults. Matches IntegrationTestBase's pattern.
        using (var scope = Factory.Services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<PermissionRepository>().SupplementDefaults();
            scope.ServiceProvider.GetRequiredService<RoleRepository>().SupplementDefaults();
            scope.ServiceProvider.GetRequiredService<OptionRepository>().SupplementDefaults();
        }

        Db = Database.CreateDbContext();
        Builder = new TestDataBuilder(Db);

        // Shrink the meeting-collab snapshot interval so tests don't have to wait
        // the production 10s for a snapshot to fire. MeetingHub clamps to a 2s
        // minimum, so this gives ~2s ticks.
        Db.SystemOptions
            .Where(o => o.Identifier == "meetings.collab.save_interval_seconds" && o.ChapterId == null)
            .Set(o => o.Value, "1")
            .Update();

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
            try { await ctx.CloseAsync(); } catch (PlaywrightException ex) {
                Console.Error.WriteLine($"E2ETestBase.TeardownBrowser: closing extra context failed (best-effort). {ex}");
            }
        }
        _extraContexts.Clear();
        if (_context != null)
            await _context.CloseAsync();
        if (_browser != null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    public virtual void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        // Factory is NOT disposed here — shared per worker via WorkerDatabase.
        Db?.Dispose();
        GC.SuppressFinalize(this);
    }
}
