using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Tests.AssemblyAssistant.Fakes;

/// <summary>Returns the given bytes, then either ends or blocks until cancelled, like a long-lived HTTP stream.</summary>
public class LineStream : Stream {
    private readonly byte[] _content;
    private readonly bool _keepOpen;
    private readonly CancellationToken _requestToken;
    private int position;

    public LineStream(byte[] content, bool keepOpen, CancellationToken requestToken) {
        _content = content;
        _keepOpen = keepOpen;
        _requestToken = requestToken;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) {
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        if (position < _content.Length) {
            var length = Math.Min(buffer.Length, _content.Length - position);
            _content.AsMemory(position, length).CopyTo(buffer);
            position += length;
            return length;
        }
        if (!_keepOpen)
            return 0;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _requestToken);
        await Task.Delay(Timeout.Infinite, linked.Token);
        return 0;
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
