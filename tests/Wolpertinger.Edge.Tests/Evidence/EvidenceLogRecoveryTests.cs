using System.Text;
using Wolpertinger.Edge.Evidence;

namespace Wolpertinger.Edge.Tests.Evidence;

public sealed class EvidenceLogRecoveryTests
{
    private const int FixedHeaderSize = 28;

    [Fact]
    public async Task TruncatedPayloadRepairsOnlyFinalTail()
    {
        using var temp = new TempDirectory();
        EvidenceReference second;
        await using (var log = await SegmentedEvidenceLog.OpenAsync(temp.Path, 4096))
        {
            _ = await log.AppendAsync(Input("first", 1));
            second = (await log.AppendAsync(Input("second-payload", 2))).Reference;
        }

        var segment = SegmentPath(temp.Path, second.SegmentNumber);
        using (var stream = new FileStream(segment, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(second.ByteOffset + FixedHeaderSize + 3);
            stream.Flush(flushToDisk: true);
        }

        var recovered = await EvidenceLogRecovery.RecoverAsync(temp.Path);
        Assert.Single(recovered);
        Assert.Equal(second.ByteOffset, new FileInfo(segment).Length);
    }

    [Fact]
    public async Task TruncatedDigestRepairsOnlyFinalTail()
    {
        using var temp = new TempDirectory();
        EvidenceReference second;
        await using (var log = await SegmentedEvidenceLog.OpenAsync(temp.Path, 4096))
        {
            _ = await log.AppendAsync(Input("first", 1));
            second = (await log.AppendAsync(Input("second", 2))).Reference;
        }

        var segment = SegmentPath(temp.Path, second.SegmentNumber);
        using (var stream = new FileStream(segment, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(second.ByteOffset + second.FrameLength - 10);
            stream.Flush(flushToDisk: true);
        }

        var recovered = await EvidenceLogRecovery.RecoverAsync(temp.Path);
        Assert.Single(recovered);
        Assert.Equal(second.ByteOffset, new FileInfo(segment).Length);
    }

    [Fact]
    public async Task CorruptedCompletedMiddleRecordFailsWithoutSkippingForward()
    {
        using var temp = new TempDirectory();
        EvidenceReference second;
        await using (var log = await SegmentedEvidenceLog.OpenAsync(temp.Path, 4096))
        {
            _ = await log.AppendAsync(Input("first", 1));
            second = (await log.AppendAsync(Input("second", 2))).Reference;
            _ = await log.AppendAsync(Input("third", 3));
        }

        var segment = SegmentPath(temp.Path, second.SegmentNumber);
        var originalLength = new FileInfo(segment).Length;
        using (var stream = new FileStream(segment, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            stream.Position = second.ByteOffset + FixedHeaderSize;
            var value = stream.ReadByte();
            Assert.NotEqual(-1, value);
            stream.Position--;
            stream.WriteByte((byte)(value ^ 0x01));
            stream.Flush(flushToDisk: true);
        }

        await Assert.ThrowsAsync<EvidenceCorruptionException>(
            () => EvidenceLogRecovery.RecoverAsync(temp.Path));
        Assert.Equal(originalLength, new FileInfo(segment).Length);
    }

    [Fact]
    public async Task CorruptionInClosedSegmentAlwaysFails()
    {
        using var temp = new TempDirectory();
        EvidenceReference first;
        await using (var log = await SegmentedEvidenceLog.OpenAsync(temp.Path, 120))
        {
            first = (await log.AppendAsync(Input("first-payload", 1))).Reference;
            _ = await log.AppendAsync(Input("second-payload", 2));
        }

        var segment = SegmentPath(temp.Path, first.SegmentNumber);
        using (var stream = new FileStream(segment, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            stream.Position = first.ByteOffset + FixedHeaderSize;
            var value = stream.ReadByte();
            Assert.NotEqual(-1, value);
            stream.Position--;
            stream.WriteByte((byte)(value ^ 0x01));
            stream.Flush(flushToDisk: true);
        }

        await Assert.ThrowsAsync<EvidenceCorruptionException>(
            () => EvidenceLogRecovery.RecoverAsync(temp.Path));
    }
    private static RawEvidenceInput Input(string payload, long millisecond)
        => new(
            RawEvidenceSourceKind.LocalJournal,
            Encoding.UTF8.GetBytes(payload),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000 + millisecond));

    private static string SegmentPath(string directory, uint segmentNumber)
        => System.IO.Path.Combine(directory, $"evidence-{segmentNumber:D8}.wlev");

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "wolpertinger-evidence-recovery-tests",
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
