using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Wolpertinger.Edge.Kernel;

internal interface IAuthorityEpochStore : IAsyncDisposable
{
    Task<ulong> NextAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthorityEpochStore : IAuthorityEpochStore
{
    private const int RecordBytes = 40;
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes("WLEP-v1");

    private readonly FileStream _stream;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ulong _lastEpoch;

    private AuthorityEpochStore(FileStream stream)
        => _stream = stream;

    public static async Task<AuthorityEpochStore> OpenAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var stream = new FileStream(
            fullPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var store = new AuthorityEpochStore(stream);
        try
        {
            await store.RecoverAsync(cancellationToken).ConfigureAwait(false);
            return store;
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ulong> NextAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var next = checked(_lastEpoch + 1);
            var record = EncodeRecord(next);
            _stream.Position = _stream.Length;
            await _stream.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            _stream.Flush(flushToDisk: true);
            _lastEpoch = next;
            return next;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        _stream.Position = 0;
        var lastEpoch = 0UL;
        var lastGoodOffset = 0L;
        var record = new byte[RecordBytes];

        while (_stream.Position < _stream.Length)
        {
            if (_stream.Length - _stream.Position < RecordBytes)
            {
                break;
            }

            await _stream.ReadExactlyAsync(record, cancellationToken).ConfigureAwait(false);
            var epoch = BinaryPrimitives.ReadUInt64BigEndian(record.AsSpan(0, 8));
            var expectedEpoch = checked(lastEpoch + 1);
            if (epoch != expectedEpoch)
            {
                throw new InvalidDataException(
                    $"Authority epoch sequence is not contiguous at epoch {epoch}.");
            }

            var expectedDigest = ComputeDigest(epoch);
            if (!CryptographicOperations.FixedTimeEquals(
                    record.AsSpan(8, 32), expectedDigest))
            {
                throw new InvalidDataException(
                    $"Authority epoch record digest mismatch at epoch {epoch}.");
            }

            lastEpoch = epoch;
            lastGoodOffset = _stream.Position;
        }

        if (_stream.Length != lastGoodOffset)
        {
            _stream.SetLength(lastGoodOffset);
            _stream.Flush(flushToDisk: true);
        }

        _lastEpoch = lastEpoch;
        _stream.Position = _stream.Length;
    }

    private static byte[] EncodeRecord(ulong epoch)
    {
        var record = new byte[RecordBytes];
        BinaryPrimitives.WriteUInt64BigEndian(record.AsSpan(0, 8), epoch);
        ComputeDigest(epoch).CopyTo(record, 8);
        return record;
    }

    private static byte[] ComputeDigest(ulong epoch)
    {
        Span<byte> epochBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(epochBytes, epoch);
        var material = new byte[Domain.Length + epochBytes.Length];
        Domain.CopyTo(material, 0);
        epochBytes.CopyTo(material.AsSpan(Domain.Length));
        return SHA256.HashData(material);
    }

    public async ValueTask DisposeAsync()
    {
        _stream.Flush(flushToDisk: true);
        await _stream.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
