using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.Mumble;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class MumbleVoicePacketTests {
    [Test]
    public async Task Opus_packet_reads_session_and_terminator() {
        // header: codec 4 (Opus) << 5 | context 0; session 300 (2-byte varint); sequence 7; size 0x2000 | 3 (2-byte varint 0xA0 0x03); 3 audio bytes
        byte[] data = [0x80, 0x81, 0x2C, 0x07, 0xA0, 0x03, 0xAA, 0xBB, 0xCC];

        var ok = MumbleVoicePacket.TryParse(data, out var packet);

        await Assert.That(ok).IsTrue();
        await Assert.That(packet.SenderSession).IsEqualTo(300u);
        await Assert.That(packet.Context).IsEqualTo(MumbleVoicePacket.ContextNormal);
        await Assert.That(packet.IsTerminator).IsTrue();
    }

    [Test]
    public async Task Opus_packet_without_terminator() {
        byte[] data = [0x80, 0x05, 0x07, 0x03, 0xAA, 0xBB, 0xCC];

        var ok = MumbleVoicePacket.TryParse(data, out var packet);

        await Assert.That(ok).IsTrue();
        await Assert.That(packet.SenderSession).IsEqualTo(5u);
        await Assert.That(packet.IsTerminator).IsFalse();
    }

    [Test]
    public async Task Ping_codec_is_rejected() {
        byte[] data = [0x20, 0x05, 0x07];

        await Assert.That(MumbleVoicePacket.TryParse(data, out _)).IsFalse();
    }

    [Test]
    public async Task Listen_context_is_reported() {
        byte[] data = [0x83, 0x05, 0x07, 0x01, 0xAA];

        var ok = MumbleVoicePacket.TryParse(data, out var packet);

        await Assert.That(ok).IsTrue();
        await Assert.That(packet.Context).IsEqualTo(MumbleVoicePacket.ContextListen);
    }

    [Test]
    public async Task Celt_zero_length_frame_is_terminator() {
        // codec 0 (CELT alpha), session 5, sequence 7, frame with 2 bytes and continuation, then a zero-length frame
        byte[] data = [0x00, 0x05, 0x07, 0x82, 0xAA, 0xBB, 0x00];

        var ok = MumbleVoicePacket.TryParse(data, out var packet);

        await Assert.That(ok).IsTrue();
        await Assert.That(packet.IsTerminator).IsTrue();
    }

    [Test]
    public async Task Truncated_packet_is_rejected() {
        byte[] data = [0x80, 0x81];

        await Assert.That(MumbleVoicePacket.TryParse(data, out _)).IsFalse();
    }
}
