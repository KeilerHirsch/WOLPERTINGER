using System.Buffers.Binary;
using System.Security.Cryptography;
using Wolpertinger.Edge.Contracts;

namespace Wolpertinger.Edge.Evidence;

internal static class EvidenceRecordFormat
{
    public const ushort FormatVersion = 1;
    public const int FixedHeaderSize = 28;
    public const int DigestSize = 32;
    public const int FrameOverhead = FixedHeaderSize + (2 * DigestSize);
    private static ReadOnlySpan<byte> Magic => "WLEV"u8;

    public static byte[] BuildHeader(
        RawEvidenceSourceKind sourceKind,
        ulong rawOrdinal,
        long observedUnixMs,
        int payloadLength)
    {
        var header = new byte[FixedHeaderSize];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), FormatVersion);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6, 2), (ushort)sourceKind);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8, 8), rawOrdinal);
        BinaryPrimitives.WriteInt64BigEndian(header.AsSpan(16, 8), observedUnixMs);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(24, 4), (uint)payloadLength);
        return header;
    }

    public static byte[] ComputeDigest(
        ReadOnlySpan<byte> previousDigest,
        ReadOnlySpan<byte> header,
        ReadOnlySpan<byte> payload)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(previousDigest);
        hash.AppendData(header);
        hash.AppendData(payload);
        return hash.GetHashAndReset();
    }

    public static string SegmentPath(string directoryPath, uint segmentNumber)
        => Path.Combine(directoryPath, $"evidence-{segmentNumber:D8}.wlev");

    public static bool TryParseSegmentNumber(string path, out uint segmentNumber)
    {
        var name = Path.GetFileName(path);
        const string prefix = "evidence-";
        const string suffix = ".wlev";
        if (!name.StartsWith(prefix, StringComparison.Ordinal)
            || !name.EndsWith(suffix, StringComparison.Ordinal)
            || name.Length != prefix.Length + 8 + suffix.Length)
        {
            segmentNumber = 0;
            return false;
        }

        return uint.TryParse(name.AsSpan(prefix.Length, 8), out segmentNumber);
    }

    public static bool IsValidSourceKind(ushort value)
        => value is >= (ushort)RawEvidenceSourceKind.LocalJournal
            and <= (ushort)RawEvidenceSourceKind.UserEntered;
}

public sealed class SegmentedEvidenceLog : IAsyncDisposable
{
    public const long DefaultSegmentSizeBytes = 16L * 1024 * 1024;

    private readonly string _directoryPath;
    private readonly long _segmentSizeBytes;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FileStream _stream;
    private uint _segmentNumber;
    private ulong _nextRawOrdinal;
    private byte[] _previousDigest;
    private bool _disposed;

    private SegmentedEvidenceLog(
        string directoryPath,
        long segmentSizeBytes,
        FileStream stream,
        uint segmentNumber,
        ulong nextRawOrdinal,
        byte[] previousDigest)
    {
        _directoryPath = directoryPath;
        _segmentSizeBytes = segmentSizeBytes;
        _stream = stream;
        _segmentNumber = segmentNumber;
        _nextRawOrdinal = nextRawOrdinal;
        _previousDigest = previousDigest;
    }

    public static async Task<SegmentedEvidenceLog> OpenAsync(
        string directoryPath,
        long segmentSizeBytes = DefaultSegmentSizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (segmentSizeBytes <= EvidenceRecordFormat.FrameOverhead)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentSizeBytes));
        }

        Directory.CreateDirectory(directoryPath);
        var state = await EvidenceLogRecovery.RecoverStateAsync(directoryPath, cancellationToken);
        var path = EvidenceRecordFormat.SegmentPath(directoryPath, state.ActiveSegmentNumber);
        var stream = OpenSegment(path);
        stream.Position = stream.Length;

        return new SegmentedEvidenceLog(
            directoryPath,
            segmentSizeBytes,
            stream,
            state.ActiveSegmentNumber,
            state.NextRawOrdinal,
            state.PreviousDigest);
    }

    public async Task<RawEvidenceReceipt> AppendAsync(
        RawEvidenceInput input,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);
        if (!EvidenceRecordFormat.IsValidSourceKind((ushort)input.SourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Unknown raw evidence source kind.");
        }

        var payload = input.Payload.ToArray();
        if (payload.LongLength > uint.MaxValue || payload.Length > int.MaxValue - EvidenceRecordFormat.FrameOverhead)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Raw evidence payload is too large.");
        }

        var frameLength = checked(EvidenceRecordFormat.FrameOverhead + payload.Length);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stream.Length > 0 && _stream.Length + frameLength > _segmentSizeBytes)
            {
                await RotateAsync().ConfigureAwait(false);
            }

            return await AppendLockedAsync(input, payload, frameLength, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RawEvidenceReceipt> AppendLockedAsync(
        RawEvidenceInput input,
        byte[] payload,
        int frameLength,
        CancellationToken cancellationToken)
    {
        var offset = _stream.Position;
        var header = EvidenceRecordFormat.BuildHeader(
            input.SourceKind,
            _nextRawOrdinal,
            input.ObservedUtc.ToUnixTimeMilliseconds(),
            payload.Length);
        var recordDigest = EvidenceRecordFormat.ComputeDigest(_previousDigest, header, payload);

        try
        {
            await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(_previousDigest, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(recordDigest, cancellationToken).ConfigureAwait(false);
            _stream.Flush(flushToDisk: true);
        }
        catch
        {
            TryRollback(offset);
            throw;
        }

        var reference = new EvidenceReference(_nextRawOrdinal, _segmentNumber, offset, frameLength);
        _nextRawOrdinal++;
        _previousDigest = recordDigest;
        return new RawEvidenceReceipt(
            reference,
            input.SourceKind,
            FixedBytes32.FromBytes(recordDigest),
            input.ObservedUtc,
            DateTimeOffset.UtcNow,
            IsDurable: true);
    }

    private async Task RotateAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
        _segmentNumber = checked(_segmentNumber + 1);
        _stream = OpenSegment(EvidenceRecordFormat.SegmentPath(_directoryPath, _segmentNumber));
    }

    private void TryRollback(long offset)
    {
        try
        {
            _stream.SetLength(offset);
            _stream.Position = offset;
            _stream.Flush(flushToDisk: true);
        }
        catch
        {
            // Startup recovery will validate and repair an incomplete final tail.
        }
    }

    private static FileStream OpenSegment(string path)
        => new(
            path,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
