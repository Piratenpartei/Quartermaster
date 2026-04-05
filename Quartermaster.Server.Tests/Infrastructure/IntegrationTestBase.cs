using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Quartermaster.Data;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests. Provides a fresh per-test database,
/// an in-process HTTP client (with automatic CSRF cookie handling), and a
/// <see cref="TestDataBuilder"/> for seeding entities.
/// </summary>
public abstract class IntegrationTestBase : IDisposable {
    private bool _disposed;

    protected IntegrationTestFactory Factory { get; }
    protected DbContext Db { get; }
    protected TestDataBuilder Builder { get; }
    protected WorkerDatabase Database { get; }

    protected IntegrationTestBase() {
        // Lease a worker DB for the lifetime of this test — pinned to this instance,
        // so async thread-hops cannot cause cross-test pollution.
        Database = TestDatabaseFixture.Acquire();
        Database.CleanAllTables();
        Factory = new IntegrationTestFactory(Database.ConnectionString);
        Db = Database.CreateDbContext();
        Builder = new TestDataBuilder(Db);
    }

    /// <summary>
    /// HttpClient with a CookieContainer (for antiforgery) and BaseAddress set.
    /// Use <see cref="AnonymousClient"/> or <see cref="AuthenticatedClient"/> for tests.
    /// </summary>
    protected HttpClient CreateClient() {
        return Factory.CreateClient(new WebApplicationFactoryClientOptions {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    /// <summary>
    /// Anonymous HttpClient — no authorization header.
    /// </summary>
    protected HttpClient AnonymousClient() => CreateClient();

    /// <summary>
    /// HttpClient with an Authorization: Bearer &lt;token&gt; header attached.
    /// Pair with <see cref="TestDataBuilder.SeedAuthenticatedUser"/>.
    /// </summary>
    protected HttpClient AuthenticatedClient(string token) {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Fetches an antiforgery token from the server and sets the X-CSRF-TOKEN header on
    /// <paramref name="client"/>. Must be called before POST/PUT/DELETE to any /api/* endpoint.
    /// The cookie is stored automatically by the client's CookieContainer.
    /// </summary>
    protected async Task AttachAntiforgeryTokenAsync(HttpClient client) {
        var response = await client.GetAsync("/api/antiforgery/token");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
    }

    /// <summary>
    /// Convenience: build a client ready to make state-changing requests as the given bearer user.
    /// Note: the antiforgery token is fetched BEFORE the bearer header is set so that it is
    /// bound to the anonymous user. This matches the fact that <c>AntiforgeryMiddleware</c>
    /// runs before authentication, so at validation time the current user is always anonymous.
    /// </summary>
    protected async Task<HttpClient> AuthenticatedClientWithCsrfAsync(string token) {
        var client = CreateClient();
        await AttachAntiforgeryTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public virtual void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        Db.Dispose();
        Factory.Dispose();
        TestDatabaseFixture.Release(Database);
        GC.SuppressFinalize(this);
    }
}
