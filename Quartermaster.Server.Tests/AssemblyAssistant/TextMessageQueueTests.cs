using System;
using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.Mumble;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class TextMessageQueueTests {
    private static readonly DateTime T0 = new(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Second_message_waits_one_second() {
        var queue = new TextMessageQueue();
        queue.Enqueue(5, "a");
        queue.Enqueue(6, "b");

        await Assert.That(queue.DequeueDue(T0)).IsNotNull();
        await Assert.That(queue.DequeueDue(T0.AddMilliseconds(900))).IsNull();
        await Assert.That(queue.DequeueDue(T0.AddMilliseconds(1000))).IsNotNull();
    }

    [Test]
    public async Task Order_is_kept() {
        var queue = new TextMessageQueue();
        queue.Enqueue(5, "a");
        queue.Enqueue(6, "b");

        var first = queue.DequeueDue(T0);
        var second = queue.DequeueDue(T0.AddSeconds(1));

        await Assert.That(first!.Value.Message).IsEqualTo("a");
        await Assert.That(second!.Value.Session).IsEqualTo(6u);
    }

    [Test]
    public async Task Empty_queue_returns_null() {
        await Assert.That(new TextMessageQueue().DequeueDue(T0)).IsNull();
    }
}
