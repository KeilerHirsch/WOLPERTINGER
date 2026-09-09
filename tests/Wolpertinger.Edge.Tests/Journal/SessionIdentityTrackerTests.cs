using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Journal;

namespace Wolpertinger.Edge.Tests.Journal;

public sealed class SessionIdentityTrackerTests
{
    [Theory]
    [InlineData("4.2.2.0", "r300000/r0", GalaxyRealm.Live)]
    [InlineData("3.8.0.0", "r123/r0", GalaxyRealm.Legacy)]
    [InlineData("4.2.2.0", "beta/test build", GalaxyRealm.BetaOrPts)]
    [InlineData("5.0.0", "r123/r0", GalaxyRealm.Live)]
    public void RealmResolverIsExplicitAndBetaMarkersTakePrecedence(
        string version,
        string build,
        GalaxyRealm expected)
        => Assert.Equal(expected, GalaxyRealmResolver.Resolve(version, build));

    [Fact]
    public void FileHeaderCreatesPendingContextWithDeterministicSessionId()
    {
        var tracker = new SessionIdentityTracker();
        var receipt = Receipt(0, "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff");
        using var json = JsonDocument.Parse("""{"event":"Fileheader","gameversion":"4.2.2.0","build":"r300000/r0","timestamp":"2026-09-08T00:00:00Z"}""");

        var result = tracker.Observe(receipt, json.RootElement);

        Assert.Equal(SessionIdentityStatus.IdentityPending, result.Status);
        Assert.Equal(GalaxyRealm.Live, result.Realm);
        Assert.Equal(ExpectedSessionId(receipt.EvidenceDigest), result.SessionId);
        Assert.Null(result.Draft);
    }

    [Fact]
    public void CommanderBindsPendingSessionAndProducesSessionBoundDraft()
    {
        var tracker = new SessionIdentityTracker();
        var header = Receipt(0, "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff");
        using var headerJson = JsonDocument.Parse("""{"event":"Fileheader","gameversion":"4.2.2.0","build":"r300000/r0","timestamp":"2026-09-08T00:00:00Z"}""");
        _ = tracker.Observe(header, headerJson.RootElement);
        var commander = Receipt(1, "111122223333444455556666777788889999aaaabbbbccccddddeeeeffff0000");
        using var commanderJson = JsonDocument.Parse("""{"event":"Commander","FID":"FTEST0001","Name":"TEST COMMANDER","timestamp":"2026-09-08T00:00:01Z"}""");

        var result = tracker.Observe(commander, commanderJson.RootElement);

        Assert.Equal(SessionIdentityStatus.SessionBound, result.Status);
        Assert.Equal("FTEST0001", result.Profile!.Fid);
        Assert.Equal(GalaxyRealm.Live, result.Profile.Realm);
        Assert.Equal(ObservationKind.SessionBound, result.Draft!.Kind);
        Assert.Equal(SourceProvenance.LocalJournal, result.Draft.Provenance);
        Assert.Equal(commander.EvidenceDigest, result.Draft.EvidenceDigest);
    }

    [Fact]
    public void FidMismatchAfterBindingFailsClosedWithoutReplacingBinding()
    {
        var tracker = BoundTracker();
        var before = tracker.CurrentBinding;
        using var mismatch = JsonDocument.Parse("""{"event":"LoadGame","FID":"FOTHER999","timestamp":"2026-09-08T00:00:02Z"}""");

        var result = tracker.Observe(Receipt(2, RepeatHex("22")), mismatch.RootElement);

        Assert.Equal(SessionIdentityStatus.IdentityConflict, result.Status);
        Assert.Null(result.Draft);
        Assert.Equal(before, tracker.CurrentBinding);
    }

    [Fact]
    public void IdentityTrackingRejectsEvidenceFromNonJournalSource()
    {
        var tracker = new SessionIdentityTracker();
        using var header = JsonDocument.Parse("""{"event":"Fileheader","gameversion":"4.3.0.1","part":1}""");
        var receipt = Receipt(0, RepeatHex("77")) with { SourceKind = RawEvidenceSourceKind.FrontierApi };

        Assert.Throws<InvalidDataException>(() => tracker.Observe(receipt, header.RootElement));
        Assert.Null(tracker.CurrentBinding);
    }
    [Fact]
    public void ContinuedJournalPartPreservesBoundSession()
    {
        var tracker = BoundTracker();
        var before = tracker.CurrentBinding;
        using var continued = JsonDocument.Parse("""{"event":"Continued","Part":2,"timestamp":"2026-09-08T01:00:00Z"}""");
        _ = tracker.Observe(Receipt(2, RepeatHex("66")), continued.RootElement);
        using var nextHeader = JsonDocument.Parse("""{"event":"Fileheader","part":2,"gameversion":"4.3.0.1","build":"r400000/r0","timestamp":"2026-09-08T01:00:01Z"}""");

        var result = tracker.Observe(Receipt(3, RepeatHex("67")), nextHeader.RootElement);

        Assert.Equal(SessionIdentityStatus.SessionBound, result.Status);
        Assert.Equal(before, tracker.CurrentBinding);
        Assert.Equal(before, new SessionBinding(result.SessionId!.Value, result.Profile!));
        Assert.Null(result.Draft);
    }

    [Fact]
    public void ContinuedPartMismatchStartsNewPendingSession()
    {
        var tracker = BoundTracker();
        var before = tracker.CurrentBinding;
        using var continued = JsonDocument.Parse("""{"event":"Continued","Part":2}""");
        _ = tracker.Observe(Receipt(2, RepeatHex("68")), continued.RootElement);
        using var nextHeader = JsonDocument.Parse("""{"event":"Fileheader","part":3,"gameversion":"4.3.0.1"}""");

        var result = tracker.Observe(Receipt(3, RepeatHex("69")), nextHeader.RootElement);

        Assert.Equal(SessionIdentityStatus.IdentityPending, result.Status);
        Assert.Null(tracker.CurrentBinding);
        Assert.NotEqual(before!.SessionId, result.SessionId);
    }

    [Fact]
    public void ContinuedRealmMismatchStartsNewPendingSession()
    {
        var tracker = BoundTracker();
        using var continued = JsonDocument.Parse("""{"event":"Continued","Part":2}""");
        _ = tracker.Observe(Receipt(2, RepeatHex("6a")), continued.RootElement);
        using var nextHeader = JsonDocument.Parse("""{"event":"Fileheader","part":2,"gameversion":"3.8.0.0"}""");

        var result = tracker.Observe(Receipt(3, RepeatHex("6b")), nextHeader.RootElement);

        Assert.Equal(SessionIdentityStatus.IdentityPending, result.Status);
        Assert.Equal(GalaxyRealm.Legacy, result.Realm);
        Assert.Null(tracker.CurrentBinding);
    }

    [Fact]
    public void FsdJumpBeforeBindingProducesNoAuthoritativeDraft()
    {
        var tracker = new SessionIdentityTracker();
        using var jump = JsonDocument.Parse("""{"event":"FSDJump","StarSystem":"Test","SystemAddress":1,"StarPos":[0,0,0],"JumpDist":1,"FuelUsed":1,"FuelLevel":10}""");

        var result = tracker.Observe(Receipt(0, RepeatHex("33")), jump.RootElement);

        Assert.Equal(SessionIdentityStatus.IdentityPending, result.Status);
        Assert.Null(result.Draft);
        Assert.Null(tracker.CurrentBinding);
    }

    private static SessionIdentityTracker BoundTracker()
    {
        var tracker = new SessionIdentityTracker();
        using var header = JsonDocument.Parse("""{"event":"Fileheader","gameversion":"4.2.2.0","build":"r300000/r0"}""");
        _ = tracker.Observe(Receipt(0, RepeatHex("01")), header.RootElement);
        using var commander = JsonDocument.Parse("""{"event":"Commander","FID":"FTEST0001"}""");
        _ = tracker.Observe(Receipt(1, RepeatHex("02")), commander.RootElement);
        return tracker;
    }

    private static RawEvidenceReceipt Receipt(ulong ordinal, string digestHex)
        => new(
            new EvidenceReference(ordinal, 0, checked((long)ordinal * 100), 100),
            RawEvidenceSourceKind.LocalJournal,
            FixedBytes32.FromHex(digestHex),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000 + (long)ordinal),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_500 + (long)ordinal),
            IsDurable: true);

    private static FixedBytes16 ExpectedSessionId(FixedBytes32 evidenceDigest)
    {
        var prefix = Encoding.UTF8.GetBytes("session-v1");
        var digest = SHA256.HashData(prefix.Concat(evidenceDigest.ToArray()).ToArray());
        return FixedBytes16.FromBytes(digest[..16]);
    }

    private static string RepeatHex(string pair)
        => string.Concat(Enumerable.Repeat(pair, 32));
}
