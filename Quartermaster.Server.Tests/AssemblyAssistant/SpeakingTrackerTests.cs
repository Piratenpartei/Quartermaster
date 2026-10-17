using System;
using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.Mumble;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class SpeakingTrackerTests {
    private static readonly DateTime T0 = new(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);

    private static MumbleVoicePacket Packet(uint session, bool terminator = false) {
        return new MumbleVoicePacket(session, MumbleVoicePacket.ContextNormal, terminator);
    }

    [Test]
    public async Task First_packet_starts() {
        var tracker = new SpeakingTracker(TimeSpan.FromMilliseconds(500));

        var started = tracker.OnPacket(Packet(5), T0);

        await Assert.That(started).IsTypeOf<MumbleSpeakingStarted>();
        await Assert.That(tracker.IsSpeaking(5)).IsTrue();
    }

    [Test]
    public async Task Terminator_stops() {
        var tracker = new SpeakingTracker(TimeSpan.FromMilliseconds(500));
        tracker.OnPacket(Packet(5), T0);

        var stopped = tracker.OnPacket(Packet(5, terminator: true), T0.AddMilliseconds(100));

        await Assert.That(stopped).IsTypeOf<MumbleSpeakingStopped>();
        await Assert.That(tracker.IsSpeaking(5)).IsFalse();
    }

    [Test]
    public async Task Silence_longer_than_hangover_stops() {
        var tracker = new SpeakingTracker(TimeSpan.FromMilliseconds(500));
        tracker.OnPacket(Packet(5), T0);

        await Assert.That(tracker.OnTick(T0.AddMilliseconds(400))).IsEmpty();
        var stopped = tracker.OnTick(T0.AddMilliseconds(600));

        await Assert.That(stopped.Count).IsEqualTo(1);
        await Assert.That(((MumbleSpeakingStopped)stopped[0]).Session).IsEqualTo(5u);
    }

    [Test]
    public async Task Packet_within_hangover_does_not_restart() {
        var tracker = new SpeakingTracker(TimeSpan.FromMilliseconds(500));
        tracker.OnPacket(Packet(5), T0);

        var second = tracker.OnPacket(Packet(5), T0.AddMilliseconds(300));

        await Assert.That(second).IsNull();
        await Assert.That(tracker.OnTick(T0.AddMilliseconds(700))).IsEmpty();
    }

    [Test]
    public async Task Terminator_without_speech_is_ignored() {
        var tracker = new SpeakingTracker(TimeSpan.FromMilliseconds(500));

        await Assert.That(tracker.OnPacket(Packet(5, terminator: true), T0)).IsNull();
    }
}
