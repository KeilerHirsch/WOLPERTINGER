using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Security.Cryptography;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Journal;

namespace Wolpertinger.Edge.Persistence;

public sealed record NormalizedLedgerEntry(
    ulong RawOrdinal,
    EvidenceReference EvidenceReference,
    FixedBytes32 EvidenceDigest,
    int NormalizerVersion,
    int NumericSemanticsVersion,
    NormalizationDisposition Disposition,
    ulong? EvidenceSequence,
    byte[]? EnvelopeBytes);

public sealed record NormalizedObservationCommit(
    ulong? EvidenceSequence,
    ObservationEnvelope? Observation,
    bool IsDurable);

public sealed class NormalizedObservationLedger : IObservationReplaySource, IAsyncDisposable
{
    private const int SchemaVersion = 1;
    private const int NormalizerVersion = 1;
    private const int NumericSemanticsVersion = 1;
    private const int DigestLength = 32;
    private const int MaximumRecordBytes = 131_072;
    private readonly FileStream _stream;
    private readonly List<NormalizedLedgerEntry> _entries = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private byte[] _previousDigest = new byte[DigestLength];
    private ulong _nextEvidenceSequence = 1;

    private NormalizedObservationLedger(FileStream stream)
        => _stream = stream;

    public IReadOnlyList<NormalizedLedgerEntry> Entries => _entries;

    public static async Task<NormalizedObservationLedger> OpenAsync(
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
        var ledger = new NormalizedObservationLedger(stream);
        try
        {
            await ledger.RecoverAsync(cancellationToken).ConfigureAwait(false);
            return ledger;
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
    public async Task<NormalizedObservationCommit> CommitAsync(
        RawEvidenceReceipt raw,
        NormalizationDisposition disposition,
        ObservationEnvelopeDraft? draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (!raw.IsDurable)
        {
            throw new InvalidOperationException("Normalized ledger requires durable raw evidence.");
        }

        ValidateCommitArguments(disposition, draft);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sequence = disposition == NormalizationDisposition.Dispatchable
                ? _nextEvidenceSequence
                : (ulong?)null;
            var observation = sequence.HasValue
                ? draft!.ToEnvelope(sequence.Value)
                : null;
            var envelopeBytes = observation is null
                ? null
                : CborContractCodec.Encode(observation);

            var entry = new NormalizedLedgerEntry(
                raw.Reference.RawOrdinal,
                raw.Reference,
                raw.EvidenceDigest,
                NormalizerVersion,
                NumericSemanticsVersion,
                disposition,
                sequence,
                envelopeBytes);
            var payload = EncodeEntry(entry);
            if (payload.Length > MaximumRecordBytes)
            {
                throw new InvalidDataException("Normalized ledger record exceeds the v1 bound.");
            }

            Span<byte> lengthPrefix = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, checked((uint)payload.Length));
            var digest = ComputeRecordDigest(_previousDigest, lengthPrefix, payload);

            _stream.Position = _stream.Length;
            await _stream.WriteAsync(lengthPrefix.ToArray(), cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(_previousDigest, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(digest, cancellationToken).ConfigureAwait(false);
            _stream.Flush(flushToDisk: true);

            _entries.Add(entry);
            _previousDigest = digest;
            if (sequence.HasValue)
            {
                _nextEvidenceSequence = checked(sequence.Value + 1);
            }

            return new NormalizedObservationCommit(sequence, observation, IsDurable: true);
        }
        finally
        {
            _gate.Release();
        }
    }
    public async IAsyncEnumerable<ObservationEnvelope> ReadObservationsAsync(
        ObservationCursor? after,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        NormalizedLedgerEntry[] snapshot;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { snapshot = _entries.ToArray(); }
        finally { _gate.Release(); }

        foreach (var entry in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.EnvelopeBytes is null) continue;
            var observation = CborContractCodec.DecodeObservation(entry.EnvelopeBytes);
            if (after is null || Compare(observation.Cursor, after.Value) > 0)
                yield return observation;
        }
    }

    private static int Compare(ObservationCursor left, ObservationCursor right)
        => left.EvidenceSequence != right.EvidenceSequence
            ? left.EvidenceSequence.CompareTo(right.EvidenceSequence)
            : left.MessageOrdinal.CompareTo(right.MessageOrdinal);
    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        _entries.Clear();
        _stream.Position = 0;
        var expectedPrevious = new byte[DigestLength];
        var nextSequence = 1UL;
        var lastGoodOffset = 0L;

        while (_stream.Position < _stream.Length)
        {
            var recordOffset = _stream.Position;
            if (_stream.Length - recordOffset < 4)
            {
                break;
            }

            var lengthPrefix = new byte[4];
            await _stream.ReadExactlyAsync(lengthPrefix, cancellationToken).ConfigureAwait(false);
            var payloadLength = BinaryPrimitives.ReadUInt32BigEndian(lengthPrefix);
            if (payloadLength is 0 or > MaximumRecordBytes)
            {
                throw new InvalidDataException($"Invalid normalized-ledger record length at offset {recordOffset}.");
            }

            var remainingFrame = checked((long)payloadLength + (2L * DigestLength));
            if (_stream.Length - _stream.Position < remainingFrame)
            {
                break;
            }
            var payload = new byte[checked((int)payloadLength)];
            var storedPrevious = new byte[DigestLength];
            var storedDigest = new byte[DigestLength];
            await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
            await _stream.ReadExactlyAsync(storedPrevious, cancellationToken).ConfigureAwait(false);
            await _stream.ReadExactlyAsync(storedDigest, cancellationToken).ConfigureAwait(false);

            if (!CryptographicOperations.FixedTimeEquals(storedPrevious, expectedPrevious))
            {
                throw new InvalidDataException($"Normalized-ledger digest chain breaks at offset {recordOffset}.");
            }

            var computedDigest = ComputeRecordDigest(expectedPrevious, lengthPrefix, payload);
            if (!CryptographicOperations.FixedTimeEquals(storedDigest, computedDigest))
            {
                throw new InvalidDataException($"Normalized-ledger record digest mismatch at offset {recordOffset}.");
            }

            var entry = DecodeEntry(payload);
            ValidateRecoveredEntry(entry, nextSequence);
            if (_entries.Count > 0 && entry.RawOrdinal <= _entries[^1].RawOrdinal)
            {
                throw new InvalidDataException("Normalized-ledger raw ordinals must be strictly increasing.");
            }

            _entries.Add(entry);
            expectedPrevious = storedDigest;
            if (entry.EvidenceSequence.HasValue)
            {
                nextSequence = checked(entry.EvidenceSequence.Value + 1);
            }
            lastGoodOffset = _stream.Position;
        }

        if (_stream.Length != lastGoodOffset)
        {
            _stream.SetLength(lastGoodOffset);
            _stream.Flush(flushToDisk: true);
        }

        _previousDigest = expectedPrevious;
        _nextEvidenceSequence = nextSequence;
        _stream.Position = _stream.Length;
    }

    private static void ValidateCommitArguments(
        NormalizationDisposition disposition,
        ObservationEnvelopeDraft? draft)
    {
        if (!Enum.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition));
        }

        if (disposition == NormalizationDisposition.Dispatchable && draft is null)
        {
            throw new ArgumentNullException(nameof(draft), "Dispatchable entries require an observation draft.");
        }

        if (disposition != NormalizationDisposition.Dispatchable && draft is not null)
        {
            throw new ArgumentException("Non-dispatchable entries cannot carry an observation draft.", nameof(draft));
        }
    }
    private static byte[] EncodeEntry(NormalizedLedgerEntry entry)
    {
        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartArray(11);
        writer.WriteInt32(SchemaVersion);
        writer.WriteUInt64(entry.RawOrdinal);
        writer.WriteUInt64(entry.EvidenceReference.SegmentNumber);
        writer.WriteInt64(entry.EvidenceReference.ByteOffset);
        writer.WriteInt32(entry.EvidenceReference.FrameLength);
        writer.WriteByteString(entry.EvidenceDigest.ToArray());
        writer.WriteInt32(entry.NormalizerVersion);
        writer.WriteInt32(entry.NumericSemanticsVersion);
        writer.WriteInt32((int)entry.Disposition);
        if (entry.EvidenceSequence.HasValue)
        {
            writer.WriteUInt64(entry.EvidenceSequence.Value);
        }
        else
        {
            writer.WriteNull();
        }

        if (entry.EnvelopeBytes is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteByteString(entry.EnvelopeBytes);
        }
        writer.WriteEndArray();
        return writer.Encode();
    }
    private static NormalizedLedgerEntry DecodeEntry(byte[] payload)
    {
        try
        {
            var reader = new CborReader(payload, CborConformanceMode.Canonical);
            if (reader.ReadStartArray() != 11)
            {
                throw new InvalidDataException("Normalized-ledger record must contain exactly 11 fields.");
            }

            if (reader.ReadInt32() != SchemaVersion)
            {
                throw new InvalidDataException("Unsupported normalized-ledger schema version.");
            }

            var rawOrdinal = reader.ReadUInt64();
            var segmentValue = reader.ReadUInt64();
            if (segmentValue > uint.MaxValue)
            {
                throw new InvalidDataException("Evidence segment number exceeds UInt32.");
            }

            var byteOffset = reader.ReadInt64();
            var frameLength = reader.ReadInt32();
            if (byteOffset < 0 || frameLength <= 0)
            {
                throw new InvalidDataException("Evidence reference contains invalid offsets.");
            }

            var evidenceDigest = ReadDigest(reader);
            var normalizerVersion = reader.ReadInt32();
            var numericSemanticsVersion = reader.ReadInt32();
            var dispositionValue = reader.ReadInt32();
            if (dispositionValue is < 0 or > 2)
            {
                throw new InvalidDataException("Unknown normalization disposition.");
            }

            var evidenceSequence = ReadNullableUInt64(reader);
            var envelopeBytes = ReadNullableByteString(reader);
            reader.ReadEndArray();
            if (reader.BytesRemaining != 0)
            {
                throw new InvalidDataException("Trailing bytes in normalized-ledger CBOR record.");
            }

            return new NormalizedLedgerEntry(
                rawOrdinal,
                new EvidenceReference(rawOrdinal, (uint)segmentValue, byteOffset, frameLength),
                evidenceDigest,
                normalizerVersion,
                numericSemanticsVersion,
                (NormalizationDisposition)dispositionValue,
                evidenceSequence,
                envelopeBytes);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is CborContentException or InvalidOperationException or OverflowException)
        {
            throw new InvalidDataException("Malformed normalized-ledger CBOR record.", ex);
        }
    }
    private static FixedBytes32 ReadDigest(CborReader reader)
    {
        var bytes = reader.ReadByteString();
        if (bytes.Length != DigestLength)
        {
            throw new InvalidDataException("Evidence digest must contain exactly 32 bytes.");
        }

        return FixedBytes32.FromBytes(bytes);
    }

    private static ulong? ReadNullableUInt64(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        return reader.ReadUInt64();
    }

    private static byte[]? ReadNullableByteString(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        return reader.ReadByteString();
    }
    private static void ValidateRecoveredEntry(NormalizedLedgerEntry entry, ulong nextSequence)
    {
        if (entry.NormalizerVersion != NormalizerVersion || entry.NumericSemanticsVersion != NumericSemanticsVersion)
        {
            throw new InvalidDataException("Normalized-ledger semantic version is unsupported.");
        }

        if (entry.EvidenceReference.RawOrdinal != entry.RawOrdinal)
        {
            throw new InvalidDataException("Normalized-ledger raw ordinal/reference mismatch.");
        }

        if (entry.Disposition == NormalizationDisposition.Dispatchable)
        {
            if (entry.EvidenceSequence != nextSequence || entry.EnvelopeBytes is null)
            {
                throw new InvalidDataException("Dispatchable normalized-ledger sequence is not contiguous.");
            }

            var envelope = CborContractCodec.DecodeObservation(entry.EnvelopeBytes);
            if (envelope.Cursor.EvidenceSequence != nextSequence
                || envelope.Cursor.MessageOrdinal != 0
                || envelope.EvidenceDigest != entry.EvidenceDigest)
            {
                throw new InvalidDataException("Normalized-ledger envelope metadata disagrees with its record.");
            }
        }
        else if (entry.EvidenceSequence.HasValue || entry.EnvelopeBytes is not null)
        {
            throw new InvalidDataException("Non-dispatchable normalized-ledger entry carries kernel data.");
        }
    }
    private static byte[] ComputeRecordDigest(
        ReadOnlySpan<byte> previousDigest,
        ReadOnlySpan<byte> lengthPrefix,
        ReadOnlySpan<byte> payload)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(previousDigest);
        hash.AppendData(lengthPrefix);
        hash.AppendData(payload);
        return hash.GetHashAndReset();
    }

    public async ValueTask DisposeAsync()
    {
        _stream.Flush(flushToDisk: true);
        await _stream.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
