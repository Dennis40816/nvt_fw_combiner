using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>
/// Read-only projection of an admitted extent, or a caller ceiling when length is unknown.
/// The adapter owns the source; disposing this view leaves the source open.
/// </summary>
internal sealed class BoundedLocalReadStream : Stream
{
    private readonly Stream _source;
    private readonly long _maximumBytes;
    private readonly long _extent;
    private readonly long _start;
    private readonly bool _exactLength;
    private readonly CancellationToken _operationCancellation;
    private long _position;
    private bool _endVerified;
    private bool _disposed;
    private IOException? _failure;

    internal BoundedLocalReadStream(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken,
        long? admittedLength = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        if (!source.CanRead)
        {
            throw new IOException("The selected source is not readable.");
        }
        if (admittedLength is < 0)
        {
            throw new IOException("The selected source has an invalid admitted length.");
        }
        if (admittedLength > maximumBytes)
        {
            throw new LocalFileTooLargeException(
                $"File length {admittedLength} exceeds the {maximumBytes}-byte limit.");
        }

        _source = source;
        _maximumBytes = maximumBytes;
        _extent = admittedLength ?? maximumBytes;
        _exactLength = admittedLength.HasValue;
        _start = source.CanSeek ? source.Position : 0;
        _operationCancellation = cancellationToken;
    }

    public override bool CanRead => !_disposed && _source.CanRead;
    public override bool CanSeek => !_disposed && _source.CanSeek;
    public override bool CanWrite => false;
    public override long Length { get { EnsureReadable(); return _extent; } }
    public override long Position
    {
        get { EnsureReadable(); return _position; }
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        EnsureReadable();
        if (buffer.IsEmpty || _endVerified)
        {
            return 0;
        }
        int count = (int)Math.Min(buffer.Length, _extent - _position);
        if (count == 0)
        {
            if (_exactLength)
            {
                return 0;
            }
            Span<byte> probe = stackalloc byte[1];
            VerifyProbe(ReadSource(probe));
            return 0;
        }

        int read = ReadSource(buffer[..count]);
        AcceptRead(read);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureReadable();
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty || _endVerified)
        {
            return 0;
        }
        int count = (int)Math.Min(buffer.Length, _extent - _position);
        if (count == 0)
        {
            if (_exactLength)
            {
                return 0;
            }
            VerifyProbe(await ReadSourceAsync(new byte[1], cancellationToken).ConfigureAwait(false));
            return 0;
        }

        int read = await ReadSourceAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
        AcceptRead(read);
        return read;
    }

    /// <summary>Checks stability before the adapter publishes any projected value.</summary>
    internal async ValueTask CompleteAsync()
    {
        EnsureReadable();
        if (_exactLength && _source.CanSeek)
        {
            long remainingLength = _source.Length - _start;
            if (remainingLength > _maximumBytes)
            {
                _failure = new LocalFileTooLargeException(
                    $"File length {remainingLength} exceeds the {_maximumBytes}-byte limit.");
                throw _failure;
            }
            if (remainingLength != _extent)
            {
                throw Fail("The selected source changed during the stable read.");
            }
            _ = _source.Seek(checked(_start + _extent), SeekOrigin.Begin);
        }
        else if (_exactLength && _position != _extent)
        {
            throw Fail("The selected source ended before its admitted extent.");
        }
        else if (!_exactLength && !_endVerified && _position != _extent)
        {
            throw Fail("The selected source has not been completely read.");
        }
        if (!_endVerified)
        {
            VerifyProbe(await ReadSourceAsync(new byte[1], CancellationToken.None).ConfigureAwait(false));
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureReadable();
        if (!_source.CanSeek)
        {
            throw new NotSupportedException("The selected source cannot seek.");
        }
        long position;
        try
        {
            position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_position + offset),
                SeekOrigin.End => checked(_extent + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            if (position < 0 || position > _extent)
            {
                throw new IOException("Seek is outside the admitted local-file extent.");
            }
            _ = _source.Seek(checked(_start + position), SeekOrigin.Begin);
        }
        catch (OverflowException exception)
        {
            throw new IOException("Seek is outside the admitted local-file extent.", exception);
        }
        _position = position;
        _endVerified = false;
        return position;
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }

    private void EnsureReadable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _operationCancellation.ThrowIfCancellationRequested();
        if (_failure is not null)
        {
            throw _failure;
        }
    }

    private int ReadSource(Span<byte> buffer)
    {
        try
        {
            int read = _source.Read(buffer);
            _operationCancellation.ThrowIfCancellationRequested();
            return read;
        }
        catch (IOException exception)
        {
            _failure = exception;
            throw;
        }
    }

    private async ValueTask<int> ReadSourceAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        using CancellationTokenSource? linked = cancellationToken.CanBeCanceled &&
            _operationCancellation.CanBeCanceled && cancellationToken != _operationCancellation
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _operationCancellation)
            : null;
        CancellationToken token = linked?.Token ??
            (cancellationToken.CanBeCanceled ? cancellationToken : _operationCancellation);
        try
        {
            int read = await _source.ReadAsync(buffer, token).ConfigureAwait(false);
            _operationCancellation.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();
            return read;
        }
        catch (IOException exception)
        {
            _failure = exception;
            throw;
        }
    }

    private void AcceptRead(int read)
    {
        if (read == 0)
        {
            if (_exactLength)
            {
                throw Fail("The selected source ended before its admitted extent.");
            }
            _endVerified = true;
        }
        _position += read;
    }

    private void VerifyProbe(int read)
    {
        if (read != 0)
        {
            _failure = _exactLength && _extent < _maximumBytes
                ? new LocalFileReadException("The selected source grew beyond its admitted extent.")
                : new LocalFileTooLargeException($"The selected source exceeds the {_maximumBytes}-byte limit.");
            throw _failure;
        }
        _endVerified = true;
    }

    private LocalFileReadException Fail(string message)
    {
        var exception = new LocalFileReadException(message);
        _failure = exception;
        return exception;
    }
}
