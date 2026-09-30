using Lunaria.Silver;
using Xunit;

namespace Lunaria.Tests;

public sealed class FrameReaderTests
{
    private static byte[] FrameBytes(FrameType type, byte[] payload, uint sequence) =>
        Frame.Create(type, payload, sequence).Encode();

    [Fact]
    public async Task ReadsSeveralFramesFromOneChunk()
    {
        var stream = new MemoryStream(
        [
            .. FrameBytes(FrameType.Heartbeat, [1, 2, 3], sequence: 0),
            .. FrameBytes(FrameType.Heartbeat, [4, 5], sequence: 1)
        ]);
        using var reader = new FrameReader(stream);

        var first = await reader.NextFrameAsync();
        var second = await reader.NextFrameAsync();

        Assert.Equal([1, 2, 3], first!.Payload);
        Assert.Equal(expected: 0u, first.Sequence);
        Assert.Equal([4, 5], second!.Payload);
        Assert.Equal(expected: 1u, second.Sequence);
        Assert.Null(await reader.NextFrameAsync());
    }

    [Fact]
    public async Task ReassemblesAFrameSplitAcrossReads()
    {
        var payload = Enumerable.Range(start: 0, count: 64).Select(i => (byte)i).ToArray();
        using var reader = new FrameReader(new DribbleStream(FrameBytes(FrameType.Heartbeat, payload, sequence: 7), chunk: 3));

        var frame = await reader.NextFrameAsync();

        Assert.Equal(payload, frame!.Payload);
        Assert.Equal(expected: 7u, frame.Sequence);
    }

    [Fact]
    public async Task GrowsPastTheInitialBufferForALargeFrame()
    {
        // Exceeds the initial 2 KiB buffer to exercise buffer growth.
        var payload = new byte[8192];
        Random.Shared.NextBytes(payload);
        using var reader = new FrameReader(new DribbleStream(FrameBytes(FrameType.Heartbeat, payload, sequence: 3), chunk: 1500));

        var frame = await reader.NextFrameAsync();

        Assert.Equal(payload, frame!.Payload);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var reader = new FrameReader(new MemoryStream());
        reader.Dispose();
        reader.Dispose();
    }

    private sealed class DribbleStream(byte[] data, int chunk) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var available = Math.Min(Math.Min(chunk, count), data.Length - _position);

            if (available <= 0)
                return 0;

            Array.Copy(data, _position, buffer, offset, available);
            _position += available;
            return available;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var available = Math.Min(Math.Min(chunk, buffer.Length), data.Length - _position);

            if (available <= 0)
                return ValueTask.FromResult(0);

            data.AsSpan(_position, available).CopyTo(buffer.Span);
            _position += available;
            return ValueTask.FromResult(available);
        }

        public override void Flush()
        {}

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
