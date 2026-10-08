using System.Net;
using System.Text.Json;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.Accreditation;

public class HttpKeycloakAdminClientTests {
    private const string Token = """{"access_token":"tok","expires_in":300}""";

    private static KeycloakSettings Settings() => new() {
        BaseUrl = "https://sso.test", AuthRealm = "master", Realm = "users", ClientId = "admin-api", ClientSecret = "s3cret"
    };

    private static HttpKeycloakAdminClient Client(StubHttpMessageHandler handler)
        => new(new HttpClient(handler), Settings(), new KeycloakTokenProvider());

    [Test]
    public async Task FindByMemberNumber_keeps_only_exact_matches_and_uses_bearer_token() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.OK, """
            [{"id":"u1","username":"a","enabled":true,"attributes":{"member_number":["12"]}},
             {"id":"u2","username":"b","enabled":true,"attributes":{"member_number":["123"]}},
             {"id":"u3","username":"c","enabled":false,"attributes":{"member_number":[" 12 "]}}]
            """);

        var users = await Client(handler).FindByMemberNumberAsync("12", CancellationToken.None);

        await Assert.That(users.Select(u => u.Id).ToList()).IsEquivalentTo(new List<string> { "u1", "u3" });
        await Assert.That(handler.Requests[0].RequestUri!.ToString()).IsEqualTo("https://sso.test/realms/master/protocol/openid-connect/token");
        await Assert.That(handler.Requests[1].RequestUri!.ToString()).StartsWith("https://sso.test/admin/realms/users/users?q=member_number%3A12");
        await Assert.That(handler.Requests[1].Headers.Authorization!.ToString()).IsEqualTo("Bearer tok");
    }

    [Test]
    public async Task Token_is_cached_between_calls() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.OK, "[]");
        handler.Enqueue(HttpStatusCode.OK, "[]");
        var client = Client(handler);

        await client.UsernameExistsAsync("a", CancellationToken.None);
        await client.UsernameExistsAsync("b", CancellationToken.None);

        await Assert.That(handler.Requests.Count).IsEqualTo(3);
    }

    [Test]
    public async Task UsernameExists_is_case_insensitive_exact() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.OK, """[{"id":"u1","username":"erika.mustermann","enabled":true}]""");

        var exists = await Client(handler).UsernameExistsAsync("Erika.Mustermann", CancellationToken.None);

        await Assert.That(exists).IsTrue();
        await Assert.That(handler.Requests[1].RequestUri!.Query).Contains("exact=true");
    }

    [Test]
    public async Task CreateUser_posts_credentials_and_attributes_and_returns_location_id() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.EnqueueWithLocation(HttpStatusCode.Created, "https://sso.test/admin/realms/users/users/new-id");

        var id = await Client(handler).CreateUserAsync(new NewKeycloakUser("erika.mustermann", "Erika", "Mustermann", null, "Segel-Wolke-Hafen-47",
            new Dictionary<string, List<string>> { ["member_number"] = ["4711"] }), CancellationToken.None);

        await Assert.That(id).IsEqualTo("new-id");
        using var body = JsonDocument.Parse(handler.RequestBodies[1]);
        var credential = body.RootElement.GetProperty("credentials")[0];
        await Assert.That(credential.GetProperty("temporary").GetBoolean()).IsFalse();
        await Assert.That(credential.GetProperty("value").GetString()).IsEqualTo("Segel-Wolke-Hafen-47");
        await Assert.That(body.RootElement.GetProperty("emailVerified").GetBoolean()).IsFalse();
        await Assert.That(body.RootElement.TryGetProperty("email", out _)).IsFalse();
    }

    [Test]
    public async Task SetAttribute_puts_full_representation_with_only_the_target_attribute_changed() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.OK, """
            {"id":"u1","username":"legacy name","email":"x@y.z","firstName":"Erika","lastName":"Mustermann","enabled":true,
             "attributes":{"member_number":["12"],"ekklesia_eligible":["false"],"other":["keep"]}}
            """);
        handler.Enqueue(HttpStatusCode.NoContent, "");

        await Client(handler).SetAttributeAsync("u1", "ekklesia_eligible", "true", CancellationToken.None);

        await Assert.That(handler.Requests[2].Method).IsEqualTo(HttpMethod.Put);
        using var body = JsonDocument.Parse(handler.RequestBodies[2]);
        await Assert.That(body.RootElement.GetProperty("username").GetString()).IsEqualTo("legacy name");
        await Assert.That(body.RootElement.GetProperty("email").GetString()).IsEqualTo("x@y.z");
        await Assert.That(body.RootElement.GetProperty("firstName").GetString()).IsEqualTo("Erika");
        await Assert.That(body.RootElement.GetProperty("lastName").GetString()).IsEqualTo("Mustermann");
        var attributes = body.RootElement.GetProperty("attributes");
        await Assert.That(attributes.GetProperty("ekklesia_eligible")[0].GetString()).IsEqualTo("true");
        await Assert.That(attributes.GetProperty("other")[0].GetString()).IsEqualTo("keep");
    }

    [Test]
    public async Task SetAttribute_never_sends_the_enabled_flag() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.OK, """
            {"id":"u1","username":"erika.mustermann","email":"x@y.z","enabled":false,
             "attributes":{"member_number":["12"],"ekklesia_eligible":["false"]}}
            """);
        handler.Enqueue(HttpStatusCode.NoContent, "");

        await Client(handler).SetAttributeAsync("u1", "ekklesia_eligible", "true", CancellationToken.None);

        using var body = JsonDocument.Parse(handler.RequestBodies[2]);
        await Assert.That(body.RootElement.TryGetProperty("enabled", out _)).IsFalse();
        await Assert.That(body.RootElement.GetProperty("email").GetString()).IsEqualTo("x@y.z");
    }

    [Test]
    public async Task Error_message_never_contains_the_request_body() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.Conflict, """{"errorMessage":"User exists with same username"}""");

        var ex = await Assert.ThrowsAsync<KeycloakException>(() => Client(handler).CreateUserAsync(
            new NewKeycloakUser("a", "A", "B", null, "Segel-Wolke-Hafen-47", []), CancellationToken.None));

        await Assert.That(ex!.StatusCode).IsEqualTo(409);
        await Assert.That(ex.Message).DoesNotContain("Segel-Wolke-Hafen-47");
    }

    [Test]
    public async Task Connection_failure_during_search_throws_KeycloakException_with_null_status_code() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.EnqueueException(new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<KeycloakException>(() => Client(handler).FindByMemberNumberAsync("12", CancellationToken.None));

        await Assert.That(ex!.StatusCode).IsNull();
        await Assert.That(ex.InnerException is HttpRequestException).IsTrue();
    }

    [Test]
    public async Task CreateUser_timeout_throws_KeycloakException_with_null_status_code() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.EnqueueException(new OperationCanceledException());

        var ex = await Assert.ThrowsAsync<KeycloakException>(() => Client(handler).CreateUserAsync(
            new NewKeycloakUser("a", "A", "B", null, "Segel-Wolke-Hafen-47", []), CancellationToken.None));

        await Assert.That(ex!.StatusCode).IsNull();
        await Assert.That(ex.InnerException is OperationCanceledException).IsTrue();
    }

    [Test]
    public async Task Error_response_captures_the_errorMessage_key_for_mapping() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.BadRequest, """{"errorMessage":"error-invalid-email"}""");

        var ex = await Assert.ThrowsAsync<KeycloakException>(() => Client(handler).UsernameExistsAsync("a", CancellationToken.None));

        await Assert.That(ex!.StatusCode).IsEqualTo(400);
        await Assert.That(ex.ErrorMessageKey).IsEqualTo("error-invalid-email");
    }

    [Test]
    public async Task Error_response_with_a_non_json_body_has_no_errorMessage_key() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.BadRequest, "not json", "text/plain");

        var ex = await Assert.ThrowsAsync<KeycloakException>(() => Client(handler).UsernameExistsAsync("a", CancellationToken.None));

        await Assert.That(ex!.ErrorMessageKey).IsNull();
    }

    [Test]
    public async Task Admin_call_retries_once_on_401_with_a_freshly_requested_token() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"access_token":"tok1","expires_in":300}""");
        handler.Enqueue(HttpStatusCode.Unauthorized, """{"error":"invalid_token"}""");
        handler.Enqueue(HttpStatusCode.OK, """{"access_token":"tok2","expires_in":300}""");
        handler.Enqueue(HttpStatusCode.OK, "[]");

        var users = await Client(handler).FindByMemberNumberAsync("12", CancellationToken.None);

        await Assert.That(users).IsEmpty();
        await Assert.That(handler.Requests.Count).IsEqualTo(4);
        await Assert.That(handler.Requests[1].Headers.Authorization!.ToString()).IsEqualTo("Bearer tok1");
        await Assert.That(handler.Requests[3].Headers.Authorization!.ToString()).IsEqualTo("Bearer tok2");
    }

    [Test]
    public async Task Unconfigured_settings_report_not_configured() {
        await Assert.That(new KeycloakSettings { BaseUrl = "https://x", Realm = "r", ClientId = "c" }.IsConfigured).IsFalse();
        await Assert.That(Settings().IsConfigured).IsTrue();
    }
}
