using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

/// <summary>
/// Mumble control channel framing: 2-byte big-endian message type, 4-byte big-endian payload length, protobuf payload.
/// Writes are not synchronised; the caller serialises them.
/// </summary>
public static class MumbleFrameCodec {
    public const int MaxPayloadLength = 8 * 1024 * 1024;
    private const int HeaderLength = 6;

    /// <summary>Throws <see cref="EndOfStreamException"/> when the stream ends and <see cref="MumbleProtocolException"/> on an oversized frame.</summary>
    public static async Task<(MumbleMessageType Type, byte[] Payload)> ReadAsync(Stream stream, CancellationToken ct) {
        var header = new byte[HeaderLength];
        await stream.ReadExactlyAsync(header, ct);

        var type = (MumbleMessageType)BinaryPrimitives.ReadUInt16BigEndian(header);
        var length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(2));
        if (length > MaxPayloadLength)
            throw new MumbleProtocolException($"Mumble frame of type {(ushort)type} announces {length} bytes, more than {MaxPayloadLength}.");

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct);
        return (type, payload);
    }

    public static Task WriteAsync(Stream stream, MumbleMessageType type, IMessage message, CancellationToken ct) {
        return WriteRawAsync(stream, type, message.ToByteArray(), ct);
    }

    public static async Task WriteRawAsync(Stream stream, MumbleMessageType type, byte[] payload, CancellationToken ct) {
        var frame = new byte[HeaderLength + payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(frame, (ushort)type);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(2), (uint)payload.Length);
        payload.CopyTo(frame, HeaderLength);

        await stream.WriteAsync(frame, ct);
        await stream.FlushAsync(ct);
    }
}
