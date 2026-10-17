using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class OpenSlidesKeyStoreTests {
    private static bool Apply(OpenSlidesKeyStore store, string json) {
        using var document = JsonDocument.Parse(json);
        return store.Apply(document.RootElement);
    }

    [Test]
    public async Task Full_map_then_delta() {
        var store = new OpenSlidesKeyStore();

        Apply(store, """{"poll/8/state": "created", "poll/8/title": "T"}""");
        Apply(store, """{"poll/8/state": "started"}""");

        await Assert.That(store.Get("poll/8/state")!.Value.GetString()).IsEqualTo("started");
        await Assert.That(store.Get("poll/8/title")!.Value.GetString()).IsEqualTo("T");
    }

    [Test]
    public async Task Null_removes_key() {
        var store = new OpenSlidesKeyStore();
        Apply(store, """{"poll/8/live_votes": {"1": null}}""");

        var changed = Apply(store, """{"poll/8/live_votes": null}""");

        await Assert.That(changed).IsTrue();
        await Assert.That(store.Get("poll/8/live_votes")).IsNull();
    }

    [Test]
    public async Task Heartbeat_changes_nothing() {
        var store = new OpenSlidesKeyStore();

        var changed = Apply(store, "{}");

        await Assert.That(changed).IsFalse();
    }
}
