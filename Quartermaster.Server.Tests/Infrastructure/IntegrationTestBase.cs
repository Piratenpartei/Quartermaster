using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests. Leases a per-worker database (with a shared
/// <see cref="IntegrationTestFactory"/>), cleans tables, and provides HTTP clients
/// + a <see cref="TestDataBuilder"/> for seeding entities.
/// The factory is shared across all tests on the same worker — only DB state is per-test.
/// </summary>
public abstract class IntegrationTestBase : IDisposable {
    private bool _disposed;

    protected IntegrationTestFactory Factory { get; }
    protected DbContext Db { get; }
    protected TestDataBuilder Builder { get; }
    protected WorkerDatabase Database { get; }

    protected IntegrationTestBase() {
        Database = TestDatabaseFixture.Acquire();
        Database.CleanAllTables();
        Factory = Database.Factory; // shared — not disposed per-test
        // Re-seed reference data (permissions, system roles, option definitions, the
        // Null Island admin division) that SupplementDefaults provides — CleanAllTables wipes
        // them, but the shared factory only runs SupplementDefaults once at startup. Null Island
        // (Id=Guid.Empty) is the FK target for user citizenship/address divisions, so app-side
        // user creation (e.g. promoting a member to officer) needs it present.
        using (var scope = Factory.Services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>().SupplementDefaults();
            scope.ServiceProvider.GetRequiredService<PermissionRepository>().SupplementDefaults();
            scope.ServiceProvider.GetRequiredService<RoleRepository>().SupplementDefaults();
            scope.ServiceProvider.GetRequiredService<OptionRepository>().SupplementDefaults();
        }
        Db = Database.CreateDbContext();
        Builder = new TestDataBuilder(Db);

        // Bump per-worker rate-limit bucket so unrelated tests don't drain each other.
        // Direct write to skip audit-log noise. Rate-limit tests override via WithWebHostBuilder.
        Db.SystemOptions
            .Where(o => o.Identifier == "auth.ratelimit.anonymous_create_permits" && o.ChapterId == null)
            .Set(o => o.Value, "10000")
            .Update();

        // Seed dummy SMTP values so the public-submit endpoints' DEBUG-only no-SMTP bypass
        // (which materializes directly when host/sender are missing) doesn't fire and skip
        // the pending-confirmation flow that the intake tests assert on. SupplementDefaults
        // only seeds OptionDefinition rows, not SystemOption values, so insert them here.
        using (var scope = Factory.Services.CreateScope()) {
            var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
            optionRepo.SetValue("email.smtp.host", null, "smtp.test.local");
            optionRepo.SetValue("email.smtp.sender_address", null, "no-reply@test.local");
        }
    }

    protected HttpClient CreateClient() {
        return Factory.CreateClient(new WebApplicationFactoryClientOptions {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    protected HttpClient AnonymousClient() => CreateClient();

    protected HttpClient AuthenticatedClient(string token) {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected async Task AttachAntiforgeryTokenAsync(HttpClient client) {
        var response = await client.GetAsync("/api/antiforgery/token");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
    }

    protected async Task<HttpClient> AuthenticatedClientWithCsrfAsync(string token) {
        var client = CreateClient();
        // Attach auth first so the antiforgery token request runs in the authenticated
        // context — identity-bound CSRF tokens must be issued and validated under the
        // same user identity.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    /// <summary>
    /// Confirms every unconfirmed pending submission, materializing the real entities (and,
    /// via the inline dispatch queue, firing notifications synchronously). Public submit
    /// endpoints only stash a pending row; tests call this to drive the rest of the flow.
    /// </summary>
    protected async Task ConfirmAllPendingSubmissionsAsync() {
        var tokens = Db.PendingSubmissions
            .Where(p => p.ConfirmedAt == null)
            .Select(p => p.Token)
            .ToList();
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        foreach (var token in tokens) {
            var response = await client.PostAsync($"/api/submissions/{token}/confirm", null);
            response.EnsureSuccessStatusCode();
        }
    }

    public virtual void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        Db.Dispose();
        // Factory is NOT disposed here — it's shared across the worker's lifetime.
        TestDatabaseFixture.Release(Database);
        GC.SuppressFinalize(this);
    }
}
