using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Lunaria.Silver;

public sealed class FrameReader : IDisposable
{
    private const int ChunkSize = 2048;
    private const int InitialPendingSize = 2048;

    private bool _disposed;
    private int _end;

    private byte[] _pending = ArrayPool<byte>.Shared.Rent(InitialPendingSize);
    private byte[] _readChunk = ArrayPool<byte>.Shared.Rent(ChunkSize);
    private int _start;

    public FrameReader(Stream stream)
    {
        Stream = stream;
    }

    public Stream Stream { get; }
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ArrayPool<byte>.Shared.Return(_readChunk, clearArray: true);
        ArrayPool<byte>.Shared.Return(_pending, clearArray: true);
        _readChunk = [];
        _pending = [];
    }

    public async Task<Frame?> NextFrameAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (TryTakeFrame(out var frame))
                return frame;

            var read = await Stream.ReadAsync(_readChunk.AsMemory(start: 0, _readChunk.Length), cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
                return null;

            Append(_readChunk.AsSpan(start: 0, read));
        }
    }

    private void Append(ReadOnlySpan<byte> bytes)
    {
        if (_end + bytes.Length > _pending.Length)
        {
            var live = _end - _start;

            if (live + bytes.Length <= _pending.Length)
            {
                Array.Copy(_pending, _start, _pending, destinationIndex: 0, live);
            } else
            {
                var grown = ArrayPool<byte>.Shared.Rent(Math.Max(_pending.Length * 2, live + bytes.Length));
                Array.Copy(_pending, _start, grown, destinationIndex: 0, live);
                ArrayPool<byte>.Shared.Return(_pending, clearArray: true);
                _pending = grown;
            }

            _start = 0;
            _end = live;
        }

        bytes.CopyTo(_pending.AsSpan(_end));
        _end += bytes.Length;
    }

    private bool TryTakeFrame([NotNullWhen(true)] out Frame? frame)
    {
        frame = null;
        var buffered = _pending.AsSpan(_start, _end - _start);
        var total = Frame.FrameLengthOf(buffered);

        if (total is null)
            return false;

        if (total.Value > Frame.MaxFrameLength)
            throw new IOException("frame exceeds maximum size, dropping peer");

        if (buffered.Length < total.Value)
            return false;

        if (!Frame.TryDecode(buffered.Slice(start: 0, total.Value), out frame))
            throw new IOException("frame failed to decode, dropping peer");

        _start += total.Value;

        if (_start == _end)
            _start = _end = 0;
        return true;
    }
}
