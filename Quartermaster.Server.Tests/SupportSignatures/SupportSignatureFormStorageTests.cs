using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Server.SupportSignatures;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class SupportSignatureFormStorageTests {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qm-forms-" + Guid.NewGuid().ToString("N"));

    [After(Test)]
    public void Cleanup() {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public async Task Overlapping_uploads_leave_one_complete_file() {
        var storage = new SupportSignatureFormStorage(_root);
        var campaignId = Guid.NewGuid();
        var slow = new GatedStream(Encoding.ASCII.GetBytes("AAAAAAAA"));

        var first = storage.SaveAsync(campaignId, slow, CancellationToken.None);
        await slow.FirstChunkRead.Task;
        await storage.SaveAsync(campaignId, new MemoryStream(Encoding.ASCII.GetBytes("BBBB")), CancellationToken.None);
        slow.Release.SetResult();
        await first;

        using var reader = new StreamReader(storage.OpenRead(campaignId)!);
        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("AAAAAAAA");
    }

    /// <summary>Hands out its first half, then waits for <see cref="Release"/> before the rest.</summary>
    private sealed class GatedStream : Stream {
        private readonly byte[] _data;
        private int _position;

        public GatedStream(byte[] data) {
            _data = data;
        }

        public TaskCompletionSource FirstChunkRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            if (_position == _data.Length / 2) {
                FirstChunkRead.TrySetResult();
                await Release.Task;
            }

            var end = _position < _data.Length / 2 ? _data.Length / 2 : _data.Length;
            var count = Math.Min(buffer.Length, end - _position);
            _data.AsMemory(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }
}
