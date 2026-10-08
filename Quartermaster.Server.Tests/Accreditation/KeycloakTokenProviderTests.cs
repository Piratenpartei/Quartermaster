using System.Net;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.Accreditation;

public class KeycloakTokenProviderTests {
    private static KeycloakSettings Settings(string clientSecret = "s3cret") => new() {
        BaseUrl = "https://sso.test", AuthRealm = "master", Realm = "users", ClientId = "admin-api", ClientSecret = clientSecret
    };

    [Test]
    public async Task Non_json_token_body_throws_KeycloakException() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "<html>maintenance</html>", "text/html");
        var provider = new KeycloakTokenProvider();

        await Assert.ThrowsAsync<KeycloakException>(() => provider.GetTokenAsync(new HttpClient(handler), Settings(), CancellationToken.None));
    }

    [Test]
    public async Task Token_response_without_expires_in_throws_KeycloakException() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"access_token":"t"}""");
        var provider = new KeycloakTokenProvider();

        await Assert.ThrowsAsync<KeycloakException>(() => provider.GetTokenAsync(new HttpClient(handler), Settings(), CancellationToken.None));
    }

    [Test]
    public async Task Different_client_secret_requests_a_new_token() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"access_token":"tok1","expires_in":300}""");
        handler.Enqueue(HttpStatusCode.OK, """{"access_token":"tok2","expires_in":300}""");
        var http = new HttpClient(handler);
        var provider = new KeycloakTokenProvider();

        var first = await provider.GetTokenAsync(http, Settings("secret-a"), CancellationToken.None);
        var second = await provider.GetTokenAsync(http, Settings("secret-b"), CancellationToken.None);

        await Assert.That(first).IsEqualTo("tok1");
        await Assert.That(second).IsEqualTo("tok2");
        await Assert.That(handler.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ForceRefresh_requests_a_new_token_even_when_the_cached_one_is_still_valid() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"access_token":"tok1","expires_in":300}""");
        handler.Enqueue(HttpStatusCode.OK, """{"access_token":"tok2","expires_in":300}""");
        var http = new HttpClient(handler);
        var provider = new KeycloakTokenProvider();

        var first = await provider.GetTokenAsync(http, Settings(), CancellationToken.None);
        var second = await provider.GetTokenAsync(http, Settings(), CancellationToken.None, forceRefresh: true);

        await Assert.That(first).IsEqualTo("tok1");
        await Assert.That(second).IsEqualTo("tok2");
        await Assert.That(handler.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Connection_failure_during_token_request_throws_KeycloakException_with_the_inner_exception() {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("connection refused"));
        var provider = new KeycloakTokenProvider();

        var ex = await Assert.ThrowsAsync<KeycloakException>(() => provider.GetTokenAsync(new HttpClient(handler), Settings(), CancellationToken.None));

        await Assert.That(ex!.StatusCode).IsNull();
        await Assert.That(ex.InnerException is HttpRequestException).IsTrue();
    }
}
