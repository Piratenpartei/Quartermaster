using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class LoginLockoutUnlockEndpointTests : IntegrationTestBase {
    private void SeedFailedAttempts(string ip, string identifier, int count) {
        for (var i = 0; i < count; i++) {
            Db.Insert(new LoginAttempt {
                Id = Guid.NewGuid(),
                IpAddress = ip,
                UsernameOrEmail = identifier,
                Success = false,
                AttemptedAt = DateTime.UtcNow.AddMinutes(-1)
            });
        }
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/users/lockouts/unlock",
            new LoginLockoutUnlockRequest { IpAddress = "1.2.3.4", UsernameOrEmail = "x" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_users_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/users/lockouts/unlock",
            new LoginLockoutUnlockRequest { IpAddress = "1.2.3.4", UsernameOrEmail = "x" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_400_when_ip_missing() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/users/lockouts/unlock",
            new LoginLockoutUnlockRequest { IpAddress = "", UsernameOrEmail = "someone" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_username_missing() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/users/lockouts/unlock",
            new LoginLockoutUnlockRequest { IpAddress = "1.2.3.4", UsernameOrEmail = "" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Clears_failed_attempts_on_success() {
        SeedFailedAttempts("10.0.0.9", "victim@example.com", 5);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/users/lockouts/unlock",
            new LoginLockoutUnlockRequest { IpAddress = "10.0.0.9", UsernameOrEmail = "victim@example.com" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var remaining = Db.LoginAttempts
            .Where(a => a.IpAddress == "10.0.0.9" && a.UsernameOrEmail == "victim@example.com" && !a.Success)
            .Count();
        await Assert.That(remaining).IsEqualTo(0);
    }
}
