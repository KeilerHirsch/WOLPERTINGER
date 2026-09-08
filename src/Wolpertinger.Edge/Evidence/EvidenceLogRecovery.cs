using System.Buffers.Binary;
using Wolpertinger.Edge.Contracts;

namespace Wolpertinger.Edge.Evidence;

internal sealed record EvidenceRecoveryState(
    uint ActiveSegmentNumber,
    ulong NextRawOrdinal,
    byte[] PreviousDigest,
    IReadOnlyList<RecoveredRawEvidence> Records);

public static class EvidenceLogRecovery
{
    public static async Task<IReadOnlyList<RecoveredRawEvidence>> RecoverAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        var state = await RecoverStateAsync(directoryPath, cancellationToken).ConfigureAwait(false);
        return state.Records;
    }

    internal static async Task<EvidenceRecoveryState> RecoverStateAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        Directory.CreateDirectory(directoryPath);

        var segments = EnumerateSegments(directoryPath);
        ValidateSegmentSequence(segments);

        var records = new List<RecoveredRawEvidence>();
        var previousDigest = new byte[EvidenceRecordFormat.DigestSize];
        ulong nextRawOrdinal = 0;

        for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segment = segments[segmentIndex];
            var isFinalSegment = segmentIndex == segments.Count - 1;
            var data = await File.ReadAllBytesAsync(segment.Path, cancellationToken).ConfigureAwait(false);
            var position = 0;

            while (position < data.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var recordStart = position;
                var remaining = data.Length - position;
                if (remaining < EvidenceRecordFormat.FixedHeaderSize)
                {
                    if (isFinalSegment)
                    {
                        TruncateTail(segment.Path, recordStart);
                        break;
                    }

                    throw Corruption(segment, recordStart, "truncated fixed header in closed segment");
                }

                var header = data.AsSpan(position, EvidenceRecordFormat.FixedHeaderSize);
                ValidateHeaderIdentity(header, segment, recordStart);
                var sourceValue = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(6, 2));
                var rawOrdinal = BinaryPrimitives.ReadUInt64BigEndian(header.Slice(8, 8));
                var observedUnixMs = BinaryPrimitives.ReadInt64BigEndian(header.Slice(16, 8));
                var payloadLength = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(24, 4));

                if (!EvidenceRecordFormat.IsValidSourceKind(sourceValue))
                {
                    throw Corruption(segment, recordStart, "unknown source kind");
                }

                if (rawOrdinal != nextRawOrdinal)
                {
                    throw Corruption(segment, recordStart, $"raw ordinal {rawOrdinal} != expected {nextRawOrdinal}");
                }

                var frameLengthLong = EvidenceRecordFormat.FrameOverhead + (long)payloadLength;
                if (frameLengthLong > int.MaxValue || frameLengthLong > remaining)
                {
                    if (isFinalSegment)
                    {
                        TruncateTail(segment.Path, recordStart);
                        break;
                    }

                    throw Corruption(segment, recordStart, "truncated record in closed segment");
                }

                var frameLength = (int)frameLengthLong;
                var payloadOffset = position + EvidenceRecordFormat.FixedHeaderSize;
                var previousOffset = payloadOffset + (int)payloadLength;
                var digestOffset = previousOffset + EvidenceRecordFormat.DigestSize;
                var payload = data.AsSpan(payloadOffset, (int)payloadLength);
                var storedPrevious = data.AsSpan(previousOffset, EvidenceRecordFormat.DigestSize);
                var storedDigest = data.AsSpan(digestOffset, EvidenceRecordFormat.DigestSize);

                var expectedDigest = EvidenceRecordFormat.ComputeDigest(previousDigest, header, payload);
                var frameEndsAtFileEnd = position + frameLength == data.Length;
                if (!storedPrevious.SequenceEqual(previousDigest)
                    || !storedDigest.SequenceEqual(expectedDigest))
                {
                    if (isFinalSegment && frameEndsAtFileEnd)
                    {
                        TruncateTail(segment.Path, recordStart);
                        break;
                    }

                    throw Corruption(segment, recordStart, "digest chain mismatch");
                }

                DateTimeOffset observedUtc;
                try
                {
                    observedUtc = DateTimeOffset.FromUnixTimeMilliseconds(observedUnixMs);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new EvidenceCorruptionException(
                        $"Invalid observed timestamp in segment {segment.Number} at offset {recordStart}: {ex.Message}");
                }

                var reference = new EvidenceReference(rawOrdinal, segment.Number, recordStart, frameLength);
                records.Add(new RecoveredRawEvidence(
                    reference,
                    (RawEvidenceSourceKind)sourceValue,
                    payload.ToArray(),
                    observedUtc,
                    FixedBytes32.FromBytes(storedPrevious),
                    FixedBytes32.FromBytes(storedDigest)));

                previousDigest = storedDigest.ToArray();
                nextRawOrdinal++;
                position += frameLength;
            }

            if (!isFinalSegment && data.Length == 0)
            {
                throw Corruption(segment, 0, "empty closed segment");
            }
        }

        return new EvidenceRecoveryState(
            segments.Count == 0 ? 0U : segments[^1].Number,
            nextRawOrdinal,
            previousDigest,
            records);
    }

    private static List<SegmentInfo> EnumerateSegments(string directoryPath)
    {
        var result = new List<SegmentInfo>();
        foreach (var path in Directory.GetFiles(directoryPath, "evidence-*.wlev"))
        {
            if (!EvidenceRecordFormat.TryParseSegmentNumber(path, out var number))
            {
                throw new EvidenceCorruptionException($"Unrecognized evidence segment name: {path}");
            }

            result.Add(new SegmentInfo(number, path));
        }

        result.Sort(static (left, right) => left.Number.CompareTo(right.Number));
        return result;
    }

    private static void ValidateSegmentSequence(IReadOnlyList<SegmentInfo> segments)
    {
        for (var index = 0; index < segments.Count; index++)
        {
            if (segments[index].Number != (uint)index)
            {
                throw new EvidenceCorruptionException(
                    $"Evidence segment sequence gap: found {segments[index].Number}, expected {index}.");
            }
        }
    }

    private static void ValidateHeaderIdentity(
        ReadOnlySpan<byte> header,
        SegmentInfo segment,
        long recordStart)
    {
        if (!header[..4].SequenceEqual("WLEV"u8))
        {
            throw Corruption(segment, recordStart, "bad magic");
        }

        var version = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(4, 2));
        if (version != EvidenceRecordFormat.FormatVersion)
        {
            throw Corruption(segment, recordStart, $"unsupported format version {version}");
        }
    }

    private static void TruncateTail(string path, long validLength)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        stream.SetLength(validLength);
        stream.Flush(flushToDisk: true);
    }

    private static EvidenceCorruptionException Corruption(
        SegmentInfo segment,
        long offset,
        string reason)
        => new($"Evidence corruption in segment {segment.Number} at offset {offset}: {reason}.");

    private sealed record SegmentInfo(uint Number, string Path);
}
