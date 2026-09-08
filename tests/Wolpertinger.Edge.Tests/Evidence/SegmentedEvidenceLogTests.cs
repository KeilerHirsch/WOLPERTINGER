using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Wolpertinger.Edge.Evidence;

namespace Wolpertinger.Edge.Tests.Evidence;

public sealed class SegmentedEvidenceLogTests
{
    [Fact]
    public async Task AppendIsDurableAndRecoveryPreservesPayloadAndOrdinal()
    {
        using var temp = new TempDirectory();
        var payload = Encoding.UTF8.GetBytes("{\"event\":\"FSDJump\"}");
        var observed = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        RawEvidenceReceipt receipt;
        await using (var log = await SegmentedEvidenceLog.OpenAsync(temp.Path, segmentSizeBytes: 4096))
        {
            receipt = await log.AppendAsync(
                new RawEvidenceInput(RawEvidenceSourceKind.LocalJournal, payload, observed));
            Assert.True(receipt.IsDurable);
            Assert.Equal(0UL, receipt.Reference.RawOrdinal);
        }

        var recovered = await EvidenceLogRecovery.RecoverAsync(temp.Path);
        var record = Assert.Single(recovered);
        Assert.Equal(payload, record.Payload);
        Assert.Equal(0UL, record.Reference.RawOrdinal);
        Assert.Equal(receipt.EvidenceDigest, record.EvidenceDigest);
    }

    [Fact]
    public async Task RotatesBeforeNextFrameWouldExceedSegmentLimit()
    {
        using var temp = new TempDirectory();
        await using var log = await SegmentedEvidenceLog.OpenAsync(temp.Path, segmentSizeBytes: 120);
        var payload = Encoding.UTF8.GetBytes("12345678901234567890");

        var first = await log.AppendAsync(Input(payload, 1));
        var second = await log.AppendAsync(Input(payload, 2));

        Assert.Equal(0U, first.Reference.SegmentNumber);
        Assert.Equal(1U, second.Reference.SegmentNumber);
        Assert.Equal(0L, first.Reference.ByteOffset);
        Assert.Equal(0L, second.Reference.ByteOffset);
        Assert.Equal(2, Directory.GetFiles(temp.Path, "evidence-*.wlev").Length);
    }

    [Fact]
    public async Task ConsecutiveRecordsExposeExactByteOffsetsAndDigestChain()
    {
        using var temp = new TempDirectory();
        RawEvidenceReceipt first;
        RawEvidenceReceipt second;
        await using (var log = await SegmentedEvidenceLog.OpenAsync(temp.Path, segmentSizeBytes: 4096))
        {
            first = await log.AppendAsync(Input(Encoding.UTF8.GetBytes("one"), 1));
            second = await log.AppendAsync(Input(Encoding.UTF8.GetBytes("two"), 2));
            Assert.Equal(first.Reference.ByteOffset + first.Reference.FrameLength, second.Reference.ByteOffset);
        }

        var recovered = await EvidenceLogRecovery.RecoverAsync(temp.Path);
        Assert.Equal(first.EvidenceDigest, recovered[1].PreviousDigest);
    }

    [Fact]
    public async Task ReopenContinuesOrdinalAndDigestChain()
    {
        using var temp = new TempDirectory();
        RawEvidenceReceipt first;
        await using (var log = await SegmentedEvidenceLog.OpenAsync(temp.Path, 4096))
        {
            first = await log.AppendAsync(Input(Encoding.UTF8.GetBytes("first"), 1));
        }

        RawEvidenceReceipt second;
        await using (var reopened = await SegmentedEvidenceLog.OpenAsync(temp.Path, 4096))
        {
            second = await reopened.AppendAsync(Input(Encoding.UTF8.GetBytes("second"), 2));
        }

        Assert.Equal(1UL, second.Reference.RawOrdinal);
        var recovered = await EvidenceLogRecovery.RecoverAsync(temp.Path);
        Assert.Equal(2, recovered.Count);
        Assert.Equal(first.EvidenceDigest, recovered[1].PreviousDigest);
        Assert.Equal(second.EvidenceDigest, recovered[1].EvidenceDigest);
    }
    [Fact]
    public async Task FrameMatchesSpecifiedBigEndianLayoutAndDigestFormula()
    {
        using var temp = new TempDirectory();
        var payload = Encoding.UTF8.GetBytes("frame-check");
        var observed = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_123_456);
        await using (var log = await SegmentedEvidenceLog.OpenAsync(temp.Path, 4096))
        {
            _ = await log.AppendAsync(new RawEvidenceInput(RawEvidenceSourceKind.LocalJournal, payload, observed));
        }

        var frame = await File.ReadAllBytesAsync(System.IO.Path.Combine(temp.Path, "evidence-00000000.wlev"));
        Assert.Equal("WLEV"u8.ToArray(), frame[..4]);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4, 2)));
        Assert.Equal((ushort)RawEvidenceSourceKind.LocalJournal, BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(6, 2)));
        Assert.Equal(0UL, BinaryPrimitives.ReadUInt64BigEndian(frame.AsSpan(8, 8)));
        Assert.Equal(observed.ToUnixTimeMilliseconds(), BinaryPrimitives.ReadInt64BigEndian(frame.AsSpan(16, 8)));
        Assert.Equal((uint)payload.Length, BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(24, 4)));

        var previous = frame.AsSpan(28 + payload.Length, 32).ToArray();
        Assert.Equal(new byte[32], previous);
        var digestInput = previous.Concat(frame.AsSpan(0, 28).ToArray()).Concat(payload).ToArray();
        Assert.Equal(SHA256.HashData(digestInput), frame.AsSpan(60 + payload.Length, 32).ToArray());
    }
    private static RawEvidenceInput Input(byte[] payload, long millisecond)
        => new(
            RawEvidenceSourceKind.LocalJournal,
            payload,
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000 + millisecond));

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "wolpertinger-evidence-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
