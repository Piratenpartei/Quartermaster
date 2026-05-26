using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Server.Messaging;

namespace Quartermaster.Server.Tests.Messaging;

public class MessageChannelRegistryTests {
    [Test]
    public async Task Get_returns_registered_channel_by_id() {
        var a = new StubChannel("alpha");
        var b = new StubChannel("beta");
        var registry = new MessageChannelRegistry(new[] { a, b });

        await Assert.That(registry.Get("alpha")).IsSameReferenceAs(a);
        await Assert.That(registry.Get("beta")).IsSameReferenceAs(b);
    }

    [Test]
    public async Task Get_returns_null_for_unknown_id() {
        var registry = new MessageChannelRegistry(new[] { new StubChannel("alpha") });
        await Assert.That(registry.Get("gamma")).IsNull();
    }

    [Test]
    public async Task All_exposes_every_registered_channel() {
        var a = new StubChannel("a");
        var b = new StubChannel("b");
        var c = new StubChannel("c");
        var registry = new MessageChannelRegistry(new[] { a, b, c });
        await Assert.That(registry.All.Count).IsEqualTo(3);
    }

    private class StubChannel : IMessageChannel {
        public StubChannel(string id) {
            Id = id;
        }
        public string Id { get; }
        public bool IsConfigured => true;
        public Task<ChannelDeliveryResult> SendAsync(ChannelMessage message, CancellationToken ct = default)
            => Task.FromResult(ChannelDeliveryResult.Ok());
    }
}
