using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Users;
using Quartermaster.Data.Tokens;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class SessionEndpointsTests : IntegrationTestBase {
    [Test]
    public async Task List_returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/sessions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task List_returns_only_callers_sessions() {
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(firstName: "Alice");
        var (_, _) = Builder.SeedAuthenticatedUser(firstName: "Bob");
        using var client = AuthenticatedClient(aliceToken);

        var response = await client.GetAsync("/api/users/sessions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var sessions = await response.Content.ReadFromJsonAsync<List<SessionDTO>>();
        await Assert.That(sessions).IsNotNull();
        await Assert.That(sessions!.Count).IsEqualTo(1);
        // The single token Alice has should be marked current.
        await Assert.That(sessions[0].IsCurrent).IsTrue();
        // And it should belong to Alice — verify via TokenRepo.
        var aliceTokenRows = Db.Tokens.Where(t => t.UserId == alice.Id).ToList();
        await Assert.That(aliceTokenRows.Count).IsEqualTo(1);
        await Assert.That(sessions[0].TokenId).IsEqualTo(aliceTokenRows[0].Id);
    }

    [Test]
    public async Task List_marks_the_current_token_only() {
        var (alice, currentToken) = Builder.SeedAuthenticatedUser(firstName: "Alice");
        // Seed a second token for Alice — same user, separate login session.
        var secondTokenRow = Db.LoginUser(alice.Id, DateTime.UtcNow.AddDays(7), "10.0.0.1", "Other UA");
        using var client = AuthenticatedClient(currentToken);

        var response = await client.GetAsync("/api/users/sessions");
        var sessions = await response.Content.ReadFromJsonAsync<List<SessionDTO>>();
        await Assert.That(sessions!.Count).IsEqualTo(2);
        await Assert.That(sessions.Count(s => s.IsCurrent)).IsEqualTo(1);
        // The "second" row that we did NOT authenticate with should NOT be current.
        await Assert.That(sessions.Single(s => s.TokenId == secondTokenRow.Id).IsCurrent).IsFalse();
    }

    [Test]
    public async Task Revoke_other_users_token_is_silently_noop() {
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(firstName: "Alice");
        var (bob, _) = Builder.SeedAuthenticatedUser(firstName: "Bob");
        var bobTokenId = Db.Tokens.Where(t => t.UserId == bob.Id).Select(t => t.Id).Single();

        using var client = await AuthenticatedClientWithCsrfAsync(aliceToken);
        var response = await client.DeleteAsync($"/api/users/sessions/{bobTokenId}");
        // Idempotent: 204 either way.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        // Bob's token must still be intact.
        var stillThere = Db.Tokens.Any(t => t.Id == bobTokenId);
        await Assert.That(stillThere).IsTrue();
    }

    [Test]
    public async Task Revoke_own_token_removes_it() {
        var (alice, currentToken) = Builder.SeedAuthenticatedUser(firstName: "Alice");
        var secondTokenRow = Db.LoginUser(alice.Id, DateTime.UtcNow.AddDays(7), null, null);

        using var client = await AuthenticatedClientWithCsrfAsync(currentToken);
        var response = await client.DeleteAsync($"/api/users/sessions/{secondTokenRow.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        var stillThere = Db.Tokens.Any(t => t.Id == secondTokenRow.Id);
        await Assert.That(stillThere).IsFalse();
    }

    [Test]
    public async Task Revoke_current_token_makes_subsequent_request_401() {
        var (_, currentToken) = Builder.SeedAuthenticatedUser(firstName: "Alice");
        using var client = await AuthenticatedClientWithCsrfAsync(currentToken);

        // Discover which Token row backs this client.
        var listResponse = await client.GetAsync("/api/users/sessions");
        var sessions = await listResponse.Content.ReadFromJsonAsync<List<SessionDTO>>();
        var currentSessionId = sessions!.Single(s => s.IsCurrent).TokenId;

        var deleteResponse = await client.DeleteAsync($"/api/users/sessions/{currentSessionId}");
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Next call on the same client should be 401 — the bearer no longer maps to a row.
        var nextResponse = await client.GetAsync("/api/users/sessions");
        await Assert.That(nextResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RevokeOthers_preserves_current_token() {
        var (alice, currentToken) = Builder.SeedAuthenticatedUser(firstName: "Alice");
        // Three extra sessions for Alice.
        var other1 = Db.LoginUser(alice.Id, DateTime.UtcNow.AddDays(7), null, null);
        var other2 = Db.LoginUser(alice.Id, DateTime.UtcNow.AddDays(7), null, null);
        var other3 = Db.LoginUser(alice.Id, DateTime.UtcNow.AddDays(7), null, null);

        using var client = await AuthenticatedClientWithCsrfAsync(currentToken);
        var response = await client.PostAsync("/api/users/sessions/revoke-others", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var aliceTokens = Db.Tokens.Where(t => t.UserId == alice.Id).ToList();
        await Assert.That(aliceTokens.Count).IsEqualTo(1);
        await Assert.That(aliceTokens.Any(t => t.Id == other1.Id)).IsFalse();
        await Assert.That(aliceTokens.Any(t => t.Id == other2.Id)).IsFalse();
        await Assert.That(aliceTokens.Any(t => t.Id == other3.Id)).IsFalse();

        // And the current bearer must still work.
        var afterResponse = await client.GetAsync("/api/users/sessions");
        await Assert.That(afterResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task RevokeOthers_does_not_touch_other_users_tokens() {
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(firstName: "Alice");
        var (bob, _) = Builder.SeedAuthenticatedUser(firstName: "Bob");
        Db.LoginUser(alice.Id, DateTime.UtcNow.AddDays(7), null, null);
        Db.LoginUser(bob.Id, DateTime.UtcNow.AddDays(7), null, null);

        using var client = await AuthenticatedClientWithCsrfAsync(aliceToken);
        var response = await client.PostAsync("/api/users/sessions/revoke-others", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var bobStillHas = Db.Tokens.Count(t => t.UserId == bob.Id);
        await Assert.That(bobStillHas).IsEqualTo(2);
    }

    [Test]
    public async Task RevokeOthers_returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsync("/api/users/sessions/revoke-others", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task List_does_not_include_expired_tokens() {
        var (alice, aliceToken) = Builder.SeedAuthenticatedUser(firstName: "Alice");
        // Insert an expired token row directly.
        Db.LoginUser(alice.Id, DateTime.UtcNow.AddDays(-1), null, null);

        using var client = AuthenticatedClient(aliceToken);
        var response = await client.GetAsync("/api/users/sessions");
        var sessions = await response.Content.ReadFromJsonAsync<List<SessionDTO>>();
        await Assert.That(sessions!.Count).IsEqualTo(1);
    }
}
