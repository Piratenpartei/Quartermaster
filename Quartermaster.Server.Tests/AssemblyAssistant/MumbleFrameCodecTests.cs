using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.Mumble.Proto;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class MumbleFrameCodecTests {
    [Test]
    public async Task Frame_round_trips() {
        using var stream = new MemoryStream();
        var message = new TextMessage { Message = "Noch 30 Sekunden" };
        message.Session.Add(42);

        await MumbleFrameCodec.WriteAsync(stream, MumbleMessageType.TextMessage, message, CancellationToken.None);
        stream.Position = 0;
        var (type, payload) = await MumbleFrameCodec.ReadAsync(stream, CancellationToken.None);

        await Assert.That(type).IsEqualTo(MumbleMessageType.TextMessage);
        var parsed = TextMessage.Parser.ParseFrom(payload);
        await Assert.That(parsed.Message).IsEqualTo("Noch 30 Sekunden");
        await Assert.That(parsed.Session).IsEquivalentTo([42u]);
    }

    [Test]
    public async Task Header_is_big_endian() {
        using var stream = new MemoryStream();
        await MumbleFrameCodec.WriteRawAsync(stream, MumbleMessageType.UserState, [1, 2, 3], CancellationToken.None);

        await Assert.That(stream.ToArray()).IsEquivalentTo(new byte[] { 0x00, 0x09, 0x00, 0x00, 0x00, 0x03, 1, 2, 3 });
    }

    [Test]
    public async Task Oversized_length_throws() {
        using var stream = new MemoryStream([0x00, 0x01, 0x7F, 0xFF, 0xFF, 0xFF]);

        await Assert.That(async () => await MumbleFrameCodec.ReadAsync(stream, CancellationToken.None))
            .Throws<MumbleProtocolException>();
    }

    [Test]
    public async Task Truncated_header_throws_end_of_stream() {
        using var stream = new MemoryStream([0x00, 0x01, 0x00]);

        await Assert.That(async () => await MumbleFrameCodec.ReadAsync(stream, CancellationToken.None))
            .Throws<EndOfStreamException>();
    }

    [Test]
    public async Task Varint_vectors_decode() {
        var vectors = new List<(byte[] Bytes, ulong Value)> {
            ([0x00], 0),
            ([0x7F], 127),
            ([0x80, 0x80], 128),
            ([0xBF, 0xFF], 16383),
            ([0xC0, 0x40, 0x00], 16384),
            ([0xDF, 0xFF, 0xFF], (1UL << 21) - 1),
            ([0xEF, 0xFF, 0xFF, 0xFF], (1UL << 28) - 1),
            ([0xF0, 0x10, 0x00, 0x00, 0x00], 1UL << 28),
            ([0xF0, 0xFF, 0xFF, 0xFF, 0xFF], uint.MaxValue),
            ([0xF4, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00], 1UL << 40),
            ([0xFD], ~1UL),
            ([0xF8, 0x05], ~5UL)
        };

        foreach (var (bytes, expected) in vectors) {
            var offset = 0;
            var ok = MumbleVarint.TryRead(bytes, ref offset, out var value);

            await Assert.That(ok).IsTrue();
            await Assert.That(value).IsEqualTo(expected);
            await Assert.That(offset).IsEqualTo(bytes.Length);
        }
    }

    [Test]
    public async Task Truncated_varint_returns_false() {
        var offset = 0;
        var ok = MumbleVarint.TryRead(new byte[] { 0xC0, 0x40 }, ref offset, out _);

        await Assert.That(ok).IsFalse();
        await Assert.That(offset).IsEqualTo(0);
    }
}
