using System;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

/// <summary>
/// The parts of a legacy-format voice packet (as tunnelled through TCP by the server) that the bot needs.
/// Audio data is never decoded.
/// </summary>
public readonly record struct MumbleVoicePacket(uint SenderSession, byte Context, bool IsTerminator) {
    public const byte CodecCeltAlpha = 0;
    public const byte CodecPing = 1;
    public const byte CodecSpeex = 2;
    public const byte CodecCeltBeta = 3;
    public const byte CodecOpus = 4;

    public const byte ContextNormal = 0;
    public const byte ContextShout = 1;
    public const byte ContextWhisper = 2;
    public const byte ContextListen = 3;

    private const ulong OpusTerminatorBit = 0x2000;

    public static bool TryParse(ReadOnlySpan<byte> tunnelPayload, out MumbleVoicePacket packet) {
        packet = default;
        if (tunnelPayload.Length < 1)
            return false;

        var header = tunnelPayload[0];
        var codec = (byte)(header >> 5);
        var context = (byte)(header & 0x1F);
        if (codec == CodecPing || codec > CodecOpus)
            return false;

        var offset = 1;
        if (!MumbleVarint.TryRead(tunnelPayload, ref offset, out var session))
            return false;
        if (!MumbleVarint.TryRead(tunnelPayload, ref offset, out _))
            return false;

        bool isTerminator;
        if (codec == CodecOpus) {
            if (!MumbleVarint.TryRead(tunnelPayload, ref offset, out var size))
                return false;
            isTerminator = (size & OpusTerminatorBit) != 0;
        } else if (!TryReadFramedTerminator(tunnelPayload, offset, out isTerminator)) {
            return false;
        }

        packet = new MumbleVoicePacket((uint)session, context, isTerminator);
        return true;
    }

    // CELT and Speex carry frames with a one-byte header (7-bit length, 0x80 = more frames follow); a zero-length frame ends the transmission.
    private static bool TryReadFramedTerminator(ReadOnlySpan<byte> data, int offset, out bool isTerminator) {
        isTerminator = false;
        while (true) {
            if (offset >= data.Length)
                return false;

            var frameHeader = data[offset];
            var length = frameHeader & 0x7F;
            if (length == 0) {
                isTerminator = true;
                return true;
            }

            offset += 1 + length;
            if ((frameHeader & 0x80) == 0)
                return true;
        }
    }
}
