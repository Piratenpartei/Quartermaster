using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

/// <summary>
/// Turns voice packets into speaking start and stop: a sender speaks from the first packet until a terminator packet
/// or until no packet arrived for the hangover time.
/// </summary>
public class SpeakingTracker {
    private readonly TimeSpan _hangover;
    private readonly Dictionary<uint, DateTime> _lastPacket = new();

    public SpeakingTracker(TimeSpan hangover) {
        _hangover = hangover;
    }

    public bool IsSpeaking(uint session) {
        return _lastPacket.ContainsKey(session);
    }

    public MumbleEvent? OnPacket(MumbleVoicePacket packet, DateTime nowUtc) {
        var speaking = _lastPacket.ContainsKey(packet.SenderSession);
        if (packet.IsTerminator) {
            if (!speaking)
                return null;

            _lastPacket.Remove(packet.SenderSession);
            return new MumbleSpeakingStopped(packet.SenderSession, nowUtc);
        }

        _lastPacket[packet.SenderSession] = nowUtc;
        return speaking ? null : new MumbleSpeakingStarted(packet.SenderSession, nowUtc);
    }

    public IReadOnlyList<MumbleEvent> OnTick(DateTime nowUtc) {
        var silent = _lastPacket
            .Where(p => nowUtc - p.Value > _hangover)
            .Select(p => p.Key)
            .ToList();

        foreach (var session in silent)
            _lastPacket.Remove(session);

        return silent
            .Select(session => (MumbleEvent)new MumbleSpeakingStopped(session, nowUtc))
            .ToList();
    }

    public MumbleEvent? Forget(uint session, DateTime nowUtc) {
        return _lastPacket.Remove(session) ? new MumbleSpeakingStopped(session, nowUtc) : null;
    }

    public void Clear() {
        _lastPacket.Clear();
    }
}
