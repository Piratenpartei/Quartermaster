using System;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Quartermaster.Data;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using TUnit.Core;
using TUnit.Playwright;

namespace Quartermaster.Server.Tests.Infrastructure;

[ParallelLimiter<E2EParallelLimit>]
public abstract class E2ETestBase : PageTest, IDisposable {
    private bool _disposed;

    protected WorkerDatabase Database => TestDatabaseFixture.ForWorker(WorkerIndex);
    protected E2ETestFactory Factory => Database.E2EFactory;
    protected string BaseUrl => Factory.BaseUrl;
    protected DbContext Db { get; private set; } = default!;
    protected TestDataBuilder Builder { get; private set; } = default!;

    public override BrowserNewContextOptions ContextOptions(TestContext testContext) {
        return new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl,
            Locale = "de-DE"
        };
    }

    protected async Task<IPage> NewAuthenticatedPageAsync(string authToken) {
        var context = await NewContext(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl,
            Locale = "de-DE"
        });
        await SetAuthCookieAsync(context, authToken);
        return await context.NewPageAsync();
    }

    protected async Task<IPage> NewAnonymousPageAsync() {
        var context = await NewContext(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl,
            Locale = "de-DE"
        });
        return await context.NewPageAsync();
    }

    /// <summary>Call before the first navigation on the default <see cref="ContextTest.Context"/>.</summary>
    protected Task InjectAuthTokenAsync(string authToken)
        => SetAuthCookieAsync(Context, authToken);

    private Task SetAuthCookieAsync(IBrowserContext context, string authToken) {
        var uri = new Uri(BaseUrl);
        return context.AddCookiesAsync(new[] {
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
    public void SetupE2EDatabase() {
        Database.CleanAllTables();

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
