using System.Text.Json;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Journal;

namespace Wolpertinger.Edge.Tests.Journal;

public sealed class JournalFixtureTests
{
    [Fact]
    public void RealShapeFixtureBindsIgnoresForwardEventAndNormalizesJump()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "journal",
            "live-v4-fsdjump-session.jsonl");
        var lines = File.ReadAllLines(path);
        Assert.Equal(5, lines.Length);

        var tracker = new SessionIdentityTracker();
        using var header = JsonDocument.Parse(lines[0]);
        var pending = tracker.Observe(Receipt(0), header.RootElement);
        Assert.Equal(SessionIdentityStatus.IdentityPending, pending.Status);
        Assert.Equal(GalaxyRealm.Live, pending.Realm);

        using var commander = JsonDocument.Parse(lines[1]);
        var bound = tracker.Observe(Receipt(1), commander.RootElement);
        Assert.Equal(SessionIdentityStatus.SessionBound, bound.Status);
        Assert.Equal(ObservationKind.SessionBound, bound.Draft!.Kind);
        using var loadGame = JsonDocument.Parse(lines[2]);
        var repeatedIdentity = tracker.Observe(Receipt(2), loadGame.RootElement);
        Assert.Equal(SessionIdentityStatus.SessionBound, repeatedIdentity.Status);
        Assert.Null(repeatedIdentity.Draft);

        using var unknown = JsonDocument.Parse(lines[3]);
        Assert.Equal(JournalEventKind.Unknown, JournalEventClassifier.Classify(unknown.RootElement));
        var ignored = tracker.Observe(Receipt(3), unknown.RootElement);
        Assert.Equal(SessionIdentityStatus.SessionBound, ignored.Status);
        Assert.Null(ignored.Draft);

        using var jump = JsonDocument.Parse(lines[4]);
        var contextOnly = tracker.Observe(Receipt(4), jump.RootElement);
        Assert.Equal(SessionIdentityStatus.SessionBound, contextOnly.Status);
        Assert.Null(contextOnly.Draft);

        var draft = FsdJumpNormalizer.Normalize(Receipt(4), jump.RootElement, tracker.CurrentBinding!);
        var payload = Assert.IsType<FsdJumpPayload>(draft.Payload);
        Assert.Equal("W. Grantler NX-42", payload.StarSystem);
        Assert.Equal(new Decimal64(55359, -3), payload.JumpDistance);
        Assert.Equal(SourceProvenance.LocalJournal, draft.Provenance);
    }

    private static RawEvidenceReceipt Receipt(ulong ordinal)
    {
        var digest = Enumerable.Repeat((byte)(ordinal + 1), 32).ToArray();
        return new RawEvidenceReceipt(
            new EvidenceReference(ordinal, 0, checked((long)ordinal * 100), 100),
            RawEvidenceSourceKind.LocalJournal,
            FixedBytes32.FromBytes(digest),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000 + (long)ordinal),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_500 + (long)ordinal),
            IsDurable: true);
    }
}
