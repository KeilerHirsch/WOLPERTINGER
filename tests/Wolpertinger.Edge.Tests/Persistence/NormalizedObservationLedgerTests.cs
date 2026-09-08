using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Journal;
using Wolpertinger.Edge.Persistence;

namespace Wolpertinger.Edge.Tests.Persistence;

public sealed class NormalizedObservationLedgerTests
{
    [Fact]
    public async Task IgnoredRawEvidenceConsumesNoSequenceAndRestartResumesAtThree()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "normalized.woln");

        // Raw ordinal 0 existed durably before normalization; opening an empty ledger simulates that crash point.
        await using (var empty = await NormalizedObservationLedger.OpenAsync(path)) { }

        await using (var ledger = await NormalizedObservationLedger.OpenAsync(path))
        {
            var ignored = await ledger.CommitAsync(Raw(0), NormalizationDisposition.Ignored, draft: null);
            var session = await ledger.CommitAsync(Raw(1), NormalizationDisposition.Dispatchable, SessionDraft(Raw(1)));
            var jump = await ledger.CommitAsync(Raw(2), NormalizationDisposition.Dispatchable, JumpDraft(Raw(2)));

            Assert.Null(ignored.EvidenceSequence);
            Assert.Equal(1UL, session.EvidenceSequence);
            Assert.Equal(2UL, jump.EvidenceSequence);
            Assert.True(ignored.IsDurable && session.IsDurable && jump.IsDurable);
        }

        await using var reopened = await NormalizedObservationLedger.OpenAsync(path);
        var next = await reopened.CommitAsync(Raw(3), NormalizationDisposition.Dispatchable, JumpDraft(Raw(3)));
        Assert.Equal(3UL, next.EvidenceSequence);
        Assert.Equal(4, reopened.Entries.Count);
    }

    [Fact]
    public async Task DispatchableCommitStoresExactCanonicalEnvelopeBytes()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "normalized.woln");
        var raw = Raw(7);

        await using var ledger = await NormalizedObservationLedger.OpenAsync(path);
        var commit = await ledger.CommitAsync(raw, NormalizationDisposition.Dispatchable, JumpDraft(raw));

        var entry = Assert.Single(ledger.Entries);
        Assert.NotNull(commit.Observation);
        Assert.Equal(CborContractCodec.Encode(commit.Observation!), entry.EnvelopeBytes);
        Assert.Equal(raw.EvidenceDigest, entry.EvidenceDigest);
        Assert.Equal(raw.Reference, entry.EvidenceReference);
        Assert.Equal(1, entry.NormalizerVersion);
        Assert.Equal(1, entry.NumericSemanticsVersion);
    }

    private static RawEvidenceReceipt Raw(ulong ordinal)
    {
        var byteValue = (byte)(ordinal + 1);
        var digest = Enumerable.Repeat(byteValue, 32).ToArray();
        return new RawEvidenceReceipt(
            new EvidenceReference(ordinal, 0, checked((long)ordinal * 100), 100),
            FixedBytes32.FromBytes(digest),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000 + (long)ordinal),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_500 + (long)ordinal),
            IsDurable: true);
    }

    private static ObservationEnvelopeDraft SessionDraft(RawEvidenceReceipt raw)
        => new(
            raw.EvidenceDigest,
            FixedBytes16.FromHex("a0a1a2a3a4a5a6a7a8a9aaabacadaeaf"),
            new ProfileKey("FTEST0001", GalaxyRealm.Live, 0),
            ObservationKind.SessionBound,
            SourceTimeUnixMs: null,
            raw.ObservedUtc.ToUnixTimeMilliseconds(),
            raw.CommitUtc.ToUnixTimeMilliseconds(),
            MessageCount: 1,
            SessionBoundPayload.Instance,
            SourceProvenance.LocalJournal);

    private static ObservationEnvelopeDraft JumpDraft(RawEvidenceReceipt raw)
        => new(
            raw.EvidenceDigest,
            FixedBytes16.FromHex("a0a1a2a3a4a5a6a7a8a9aaabacadaeaf"),
            new ProfileKey("FTEST0001", GalaxyRealm.Live, 0),
            ObservationKind.FsdJump,
            SourceTimeUnixMs: 1_700_000_000_000,
            raw.ObservedUtc.ToUnixTimeMilliseconds(),
            raw.CommitUtc.ToUnixTimeMilliseconds(),
            MessageCount: 1,
            new FsdJumpPayload("Test", 1, new GalacticPosition(new(1, 0), new(2, 0), new(3, 0)), new(4, 0), new(5, 0), new(6, 0)),
            SourceProvenance.LocalJournal);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "wolpertinger-normalized-ledger-tests",
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
