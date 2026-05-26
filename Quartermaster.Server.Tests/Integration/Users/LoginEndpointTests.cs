using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.Users;
using Quartermaster.Data.Tokens;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class LoginEndpointTests : IntegrationTestBase {
    private const string ValidPassword = "StrongPassword123!";

    private async Task<HttpClient> AnonymousClientWithCsrfAsync() {
        var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    [Test]
    public async Task Happy_path_returns_user_info_and_sets_auth_cookie() {
        var user = Builder.SeedUser(username: "alice", password: ValidPassword);
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = "alice",
            Password = ValidPassword
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(dto).IsNotNull();
        await Assert.That(dto!.User.Id).IsEqualTo(user.Id);
        var setCookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault(c => c.Contains(".Quartermaster.Auth"));
        await Assert.That(setCookie).IsNotNull();
        var lower = setCookie!.ToLowerInvariant();
        await Assert.That(lower).Contains("httponly");
        await Assert.That(lower).Contains("samesite=strict");
    }

    [Test]
    public async Task Cookie_from_login_authenticates_subsequent_request() {
        Builder.SeedUser(username: "claire", password: ValidPassword);
        using var client = await AnonymousClientWithCsrfAsync();
        var loginResp = await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = "claire",
            Password = ValidPassword
        });
        await Assert.That(loginResp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        // HttpClient automatically stores the Set-Cookie and resends it.
        var sessionResp = await client.GetAsync("/api/users/session");
        await Assert.That(sessionResp.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Logout_clears_cookie_and_revokes_token() {
        var user = Builder.SeedUser(username: "denise", password: ValidPassword);
        using var client = await AnonymousClientWithCsrfAsync();
        await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = "denise",
            Password = ValidPassword
        });

        // Antiforgery binds tokens to user identity — refresh after login so the logout call
        // validates under the now-authenticated context.
        await AttachAntiforgeryTokenAsync(client);
        var logoutResp = await client.PostAsync("/api/users/logout", null);
        await Assert.That(logoutResp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        // Subsequent session request should fail since cookie was cleared and the token row was deleted.
        var sessionResp = await client.GetAsync("/api/users/session");
        await Assert.That(sessionResp.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        // Token row should be gone.
        await Assert.That(Db.Tokens.Any(t => t.UserId == user.Id)).IsFalse();
    }

    [Test]
    public async Task Returns_401_for_wrong_password() {
        Builder.SeedUser(username: "bob", password: ValidPassword);
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = "bob",
            Password = "WrongPasswordXYZ!!"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_401_for_nonexistent_user() {
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = "ghost",
            Password = ValidPassword
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_400_when_password_too_short() {
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = "alice",
            Password = "short"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_username_and_email_both_empty() {
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = null,
            Email = null,
            Password = ValidPassword
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Successful_login_records_issuer_ip_and_user_agent_on_token() {
        var user = Builder.SeedUser(username: "dave", password: ValidPassword);
        using var client = await AnonymousClientWithCsrfAsync();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QuartermasterTest/1.0");

        var before = DateTime.UtcNow;
        var response = await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = "dave",
            Password = ValidPassword
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var stored = Db.Tokens
            .Where(t => t.UserId == user.Id && t.Type == TokenType.Login)
            .OrderByDescending(t => t.IssuedAt)
            .First();
        await Assert.That(stored.IssuedAt).IsGreaterThanOrEqualTo(before.AddSeconds(-1));
        await Assert.That(string.IsNullOrEmpty(stored.IssuedIp)).IsFalse();
        await Assert.That(stored.IssuedUserAgent).IsEqualTo("QuartermasterTest/1.0");
    }

    [Test]
    public async Task Rotating_X_Forwarded_For_does_not_bypass_per_IP_lockout() {
        Builder.SeedUser(username: "erin", password: ValidPassword);
        using var client = await AnonymousClientWithCsrfAsync();

        // Five failed attempts, each with a different spoofed X-Forwarded-For. Pre-fix,
        // each spoofed IP would key a separate lockout bucket and never trigger. Post-fix,
        // the header is untrusted and the connection-level IP is used → all five share a bucket.
        for (var i = 0; i < 5; i++) {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/login") {
                Content = JsonContent.Create(new LoginRequest { Username = "erin", Password = "WrongPasswordXYZ!!" })
            };
            req.Headers.Add("X-Forwarded-For", $"203.0.113.{i + 1}");
            await client.SendAsync(req);
        }

        using var attempt = new HttpRequestMessage(HttpMethod.Post, "/api/users/login") {
            Content = JsonContent.Create(new LoginRequest { Username = "erin", Password = ValidPassword })
        };
        attempt.Headers.Add("X-Forwarded-For", "203.0.113.99");
        var response = await client.SendAsync(attempt);
        await Assert.That((int)response.StatusCode).IsEqualTo(429);
    }

    [Test]
    public async Task Trusted_proxy_honors_X_Forwarded_For_so_distinct_real_ips_get_distinct_lockout_buckets() {
        Builder.SeedUser(username: "frank", password: ValidPassword);

        // TestServer's loopback IP (127.0.0.1) acts as the "proxy" in this test.
        using var trusted = Factory.WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1"
            }))
        );
        using var client = trusted.CreateClient();
        await AttachAntiforgeryTokenAsync(client);

        for (var i = 0; i < 5; i++) {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/login") {
                Content = JsonContent.Create(new LoginRequest { Username = "frank", Password = "WrongPasswordXYZ!!" })
            };
            req.Headers.Add("X-Forwarded-For", "198.51.100.1");
            await client.SendAsync(req);
        }

        // Same user but a different forwarded IP ⇒ different lockout bucket ⇒ correct password lets us through.
        using var attempt = new HttpRequestMessage(HttpMethod.Post, "/api/users/login") {
            Content = JsonContent.Create(new LoginRequest { Username = "frank", Password = ValidPassword })
        };
        attempt.Headers.Add("X-Forwarded-For", "198.51.100.2");
        var response = await client.SendAsync(attempt);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Returns_429_after_5_failed_attempts() {
        Builder.SeedUser(username: "carol", password: ValidPassword);
        using var client = await AnonymousClientWithCsrfAsync();
        // 5 failed attempts
        for (var i = 0; i < 5; i++) {
            await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
                Username = "carol",
                Password = "WrongPasswordXYZ!!"
            });
        }
        // Next attempt — even correct password — should be locked out.
        var response = await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = "carol",
            Password = ValidPassword
        });
        await Assert.That((int)response.StatusCode).IsEqualTo(429);
    }
}
