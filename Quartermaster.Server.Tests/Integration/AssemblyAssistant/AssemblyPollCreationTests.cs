using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.Events;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyAssistant.Session;
using Quartermaster.Server.AssemblyAssistant.Templates;
using Quartermaster.Server.AssemblyMotions.OpenSlides;
using Quartermaster.Server.Tests.AssemblyAssistant;
using Quartermaster.Server.Tests.AssemblyAssistant.Fakes;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyAssistant;

[NotInParallel("FakeOpenSlidesClient")]
public class AssemblyPollCreationTests : IntegrationTestBase {
    private WebApplicationFactory<Program>? _host;

    [Before(Test)]
    public void Setup() {
        FakeOpenSlidesClient.Reset();
    }

    [After(Test)]
    public async Task Teardown() {
        if (_host == null)
            return;

        var sessions = _host.Services.GetRequiredService<AssemblySessionHost>();
        if (sessions.IsRunning)
            await sessions.EndSessionAsync(Guid.NewGuid(), CancellationToken.None);
        await _host.DisposeAsync();
    }

    private async Task<AssemblySessionHost> StartAsync(string map) {
        AssemblySessionHostTests.ConfigureMumble(Factory.Services);
        using (var scope = Factory.Services.CreateScope()) {
            var options = scope.ServiceProvider.GetRequiredService<OptionRepository>();
            options.SetValue("openslides.url", null, "https://openslides.invalid");
            options.SetValue("openslides.username", null, "bot");
            options.SetValue("openslides.password", null, "secret");
            options.SetValue("assembly.openslides.delegates_group_id", null, "5");
        }

        var evt = Builder.SeedEvent(Builder.SeedChapter().Id, status: EventStatus.Active);
        Db.Insert(new AssemblyMotionPortal {
            EventId = evt.Id,
            OpensAt = DateTime.UtcNow.AddDays(-10),
            ClosesAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            OpenSlidesMeetingId = OpenSlidesFixtures.MeetingId
        });

        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IMumbleConnectionFactory>();
            services.AddSingleton<IMumbleConnectionFactory>(new FakeMumbleConnectionFactory());
            services.RemoveAll<IOpenSlidesClient>();
            services.AddSingleton<IOpenSlidesClient, FakeOpenSlidesClient>();
            services.RemoveAll<IOpenSlidesAutoupdateClientFactory>();
            services.AddSingleton<IOpenSlidesAutoupdateClientFactory>(new FakeAutoupdateClientFactory(map));
        }));

        var host = _host.Services.GetRequiredService<AssemblySessionHost>();
        var started = await host.StartSessionAsync(evt.Id, Guid.NewGuid(), CancellationToken.None);
        await Assert.That(started.Accepted).IsTrue();
        await WaitForAsync(host, s => s.OpenSlidesState == "Connected" && s.AgendaItems.Count > 0);
        host.Execute(host.EventId!.Value, (state, now) => state.SelectAgendaItem(state.Version, 20, Guid.NewGuid(), now));
        return host;
    }

    private static async Task<AssemblySnapshotDTO> WaitForAsync(AssemblySessionHost host, Func<AssemblySnapshotDTO, bool> predicate) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true) {
            var snapshot = host.Snapshot();
            if (snapshot != null && predicate(snapshot))
                return snapshot;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The expected session state was not reached.");
            await Task.Delay(20);
        }
    }

    [Test]
    public async Task Template_poll_on_existing_procedural_topic_is_created_and_started() {
        var host = await StartAsync(OpenSlidesFixtures.FullMap);

        var result = await host.CreatePollAsync(host.EventId!.Value, host.Snapshot()!.Version, new PollTemplateRequest(PollTemplate.ContinueDebate, null, null), Guid.NewGuid(), CancellationToken.None);

        await Assert.That(result.Accepted).IsTrue();
        var poll = FakeOpenSlidesClient.CreatedPolls.Single();
        await Assert.That(poll.ContentObjectId).IsEqualTo("topic/30");
        await Assert.That(poll.MeetingId).IsEqualTo(3);
        await Assert.That(poll.EntitledGroupId).IsEqualTo(5);
        await Assert.That(FakeOpenSlidesClient.Topics).IsEmpty();
        await Assert.That(FakeOpenSlidesClient.PollActions).Contains(("poll.start", 100));
        var snapshot = await WaitForAsync(host, s => s.Notices.Any(n => n.MessageKey.StartsWith("ui.assembly_console.notice.poll_created")));
        await Assert.That(snapshot.ItemTitle).IsEqualTo("4.1 SÄA001: Änderung der Geschäftsordnung");
    }

    [Test]
    public async Task Missing_procedural_topic_is_created_first() {
        var host = await StartAsync(OpenSlidesFixtures.FullMap.Replace("\"meeting/3/agenda_item_ids\": [20, 21]", "\"meeting/3/agenda_item_ids\": [20]"));

        var result = await host.CreatePollAsync(host.EventId!.Value, host.Snapshot()!.Version, new PollTemplateRequest(PollTemplate.GoMotion, "Schluss der Debatte", null), Guid.NewGuid(), CancellationToken.None);

        await Assert.That(result.Accepted).IsTrue();
        var topic = FakeOpenSlidesClient.Topics.Single();
        await Assert.That(topic.Title).IsEqualTo("SÄA001: Verfahren");
        await Assert.That(topic.ParentId).IsEqualTo(20);
        await Assert.That(FakeOpenSlidesClient.CreatedPolls.Single().ContentObjectId).IsEqualTo("topic/100");
    }

    [Test]
    public async Task Final_vote_goes_on_the_motion_and_auto_start_off_leaves_it_created() {
        var host = await StartAsync(OpenSlidesFixtures.FullMap);
        host.Execute(host.EventId!.Value, (state, now) => state.SetPollSettings(PollSettings.Default with { AutoStart = false }, Guid.NewGuid(), now));

        var result = await host.CreatePollAsync(host.EventId!.Value, host.Snapshot()!.Version, new PollTemplateRequest(PollTemplate.FinalVote, null, null), Guid.NewGuid(), CancellationToken.None);

        await Assert.That(result.Accepted).IsTrue();
        var poll = FakeOpenSlidesClient.CreatedPolls.Single();
        await Assert.That(poll.ContentObjectId).IsEqualTo("motion/12");
        await Assert.That(poll.PollMethod).IsEqualTo("YNA");
        await Assert.That(FakeOpenSlidesClient.PollActions).IsEmpty();
    }

    [Test]
    public async Task OpenSlides_refusal_comes_back_as_rejection() {
        var host = await StartAsync(OpenSlidesFixtures.FullMap);
        FakeOpenSlidesClient.PollFailure = new OpenSlidesException("Keine Berechtigung", "action_failed");

        var result = await host.CreatePollAsync(host.EventId!.Value, host.Snapshot()!.Version, new PollTemplateRequest(PollTemplate.ContinueDebate, null, null), Guid.NewGuid(), CancellationToken.None);

        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(result.MessageKey).StartsWith("ui.assembly_console.reject.openslides_failed");
        await Assert.That(result.MessageKey).Contains("Keine");
    }
}
