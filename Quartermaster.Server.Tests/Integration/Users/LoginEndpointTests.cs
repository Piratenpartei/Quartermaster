using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.Users;
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
    public async Task Happy_path_returns_token_and_user_info() {
        var user = Builder.SeedUser(username: "alice", password: ValidPassword);
        using var client = await AnonymousClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/users/login", new LoginRequest {
            Username = "alice",
            Password = ValidPassword
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(dto).IsNotNull();
        await Assert.That(string.IsNullOrEmpty(dto!.Token)).IsFalse();
        await Assert.That(dto.User.Id).IsEqualTo(user.Id);
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
            EMail = null,
            Password = ValidPassword
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
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
