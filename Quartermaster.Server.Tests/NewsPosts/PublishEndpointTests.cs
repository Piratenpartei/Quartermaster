using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinqToDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Data.Options;
using Quartermaster.Server.NewsPosts.Postiz;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

[NotInParallel("FakePostizClient")]
public class PublishEndpointTests : IntegrationTestBase {
    private WebApplicationFactory<Program> _host = default!;
    private string _token = "";
    private NewsItem _item = default!;
    private PostCandidate _candidate = default!;

    [Before(Test)]
    public void Setup() {
        FakePostizClient.Reset();
        FakePostizClient.Channels.Add(new PostizChannel("x1", "Piraten X", "x", "@piraten", null, false));
        FakePostizClient.Channels.Add(new PostizChannel("m1", "Piraten Mastodon", "mastodon", null, null, false));
        FakePostizClient.Channels.Add(new PostizChannel("off", "Alt", "bluesky", null, null, true));

        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IPostizClient>();
            services.AddScoped<IPostizClient, FakePostizClient>();
        }));

        (_, _token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ReviewNewsPosts]);
        SetPostizOptions("https://postiz.test/api", "pk");

        var feed = new NewsFeed { Name = "f", Url = "https://f", Kind = NewsFeedKind.Rss };
        Db.Insert(feed);
        var now = DateTime.UtcNow;
        _item = new NewsItem {
            FeedId = feed.Id, ExternalId = "e", Url = "https://x", Title = "T", Teaser = "",
            PublishedAt = now, FetchedAt = now, UpdatedAt = now, Status = NewsItemStatus.Generated
        };
        Db.Insert(_item);
        _candidate = new PostCandidate { NewsItemId = _item.Id, Text = "Vorschlag", SectionId = Guid.NewGuid(), CreatedAt = now };
        Db.Insert(_candidate);
    }

    [After(Test)]
    public void TearDown() {
        _host.Dispose();
    }

    private async Task<HttpClient> ClientAsync() {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        var tokenResponse = await client.GetFromJsonAsync<JsonElement>("/api/antiforgery/token");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenResponse.GetProperty("token").GetString());
        return client;
    }

    private Task<HttpResponseMessage> Publish(HttpClient client, PublishRequest request)
        => client.PostAsJsonAsync($"/api/newsposts/items/{_item.Id}/publish", request);

    private void SetPostizOptions(string baseUrl, string apiKey) {
        using var scope = Factory.Services.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        optionRepo.SetValue("newsposts.postiz.base_url", null, baseUrl);
        optionRepo.SetValue("newsposts.postiz.api_key", null, apiKey);
    }

    [Test]
    public async Task Publishes_to_selected_channels_and_marks_item_posted() {
        using var client = await ClientAsync();

        var response = await Publish(client, new PublishRequest { CandidateId = _candidate.Id, Text = "Bearbeitet 🏴‍☠️", ChannelIds = ["x1", "m1"] });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(FakePostizClient.Created.Single().ChannelIds).IsEquivalentTo(new List<string> { "x1", "m1" });
        await Assert.That(Db.NewsItems.Single(i => i.Id == _item.Id).Status).IsEqualTo(NewsItemStatus.Posted);
        var publication = Db.PostPublications.Single();
        await Assert.That(publication.Status).IsEqualTo(PostPublicationStatus.Sent);
        await Assert.That(publication.FinalText).IsEqualTo("Bearbeitet 🏴‍☠️");
    }

    [Test]
    public async Task Second_publish_of_same_item_is_rejected_with_409() {
        using var client = await ClientAsync();
        await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"] });

        var second = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"] });

        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(FakePostizClient.Created.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Text_over_limit_counted_in_text_elements_is_rejected() {
        using var client = await ClientAsync();
        var tooLong = new string('x', 280) + "👍🏽";

        var response = await Publish(client, new PublishRequest { Text = tooLong, ChannelIds = ["x1"] });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(FakePostizClient.Created).IsEmpty();
    }

    [Test]
    public async Task Disabled_or_unknown_channel_is_rejected_and_item_stays_generated() {
        using var client = await ClientAsync();

        var disabled = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["off"] });
        var unknown = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["gone"] });

        await Assert.That(disabled.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(FakePostizClient.Created).IsEmpty();
        await Assert.That(Db.NewsItems.Single(i => i.Id == _item.Id).Status).IsEqualTo(NewsItemStatus.Generated);
    }

    [Test]
    public async Task Past_schedule_is_rejected() {
        using var client = await ClientAsync();

        var response = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"], ScheduledFor = DateTimeOffset.UtcNow.AddMinutes(-5) });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Future_schedule_is_forwarded_as_utc() {
        using var client = await ClientAsync();
        var at = new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1).Date.AddHours(10), DateTimeKind.Unspecified), TimeSpan.FromHours(2));

        await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["m1"], ScheduledFor = at });

        await Assert.That(FakePostizClient.Created.Single().At).IsEqualTo(at.UtcDateTime);
    }

    [Test]
    public async Task Postiz_failure_records_failed_publication_and_returns_item_to_generated() {
        using var client = await ClientAsync();
        FakePostizClient.FailNext = true;

        var response = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"] });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
        await Assert.That(Db.PostPublications.Single().Status).IsEqualTo(PostPublicationStatus.Failed);
        await Assert.That(Db.NewsItems.Single(i => i.Id == _item.Id).Status).IsEqualTo(NewsItemStatus.Generated);
    }

    [Test]
    public async Task Publish_without_postiz_configured_is_rejected_with_409() {
        SetPostizOptions("", "");
        using var client = await ClientAsync();

        var response = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"] });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(FakePostizClient.Created).IsEmpty();
        await Assert.That(Db.NewsItems.Single(i => i.Id == _item.Id).Status).IsEqualTo(NewsItemStatus.Generated);
    }

    [Test]
    public async Task Unexpected_exception_from_postiz_records_failed_publication_and_returns_item_to_generated() {
        using var client = await ClientAsync();
        FakePostizClient.ThrowNext = new InvalidOperationException("unexpected");

        var response = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"] });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
        await Assert.That(Db.PostPublications.Single().Status).IsEqualTo(PostPublicationStatus.Failed);
        await Assert.That(Db.NewsItems.Single(i => i.Id == _item.Id).Status).IsEqualTo(NewsItemStatus.Generated);
    }
}
