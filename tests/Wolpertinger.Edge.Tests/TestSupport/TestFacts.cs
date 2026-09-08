using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Facts;

namespace Wolpertinger.Edge.Tests.TestSupport;

internal static class TestFacts
{
    internal static JumpFact Jump()
        => new(
            new ObservationCursor(2, 0),
            new ProfileKey("F100", GalaxyRealm.Live, 7),
            new EvidenceReference(4, 0, 400, 100),
            FixedBytes32.FromHex("202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f"),
            FixedBytes32.FromHex("c71b67e5b6a22923473ff6d3426a927b0d6c854fec911256b8c77b4912f897cf"),
            1_234_567_890_123_456_789,
            "W. Grantler NX-42",
            new GalacticPosition(new Decimal64(12_345, -3), new Decimal64(-6_789, -2), new Decimal64(42, 0)),
            new Decimal64(55_359, -3),
            new Decimal64(4_843_642, -6),
            new Decimal64(27_123, -3),
            SourceProvenance.LocalJournal,
            FreshnessState.Current,
            SourceProvenance.LocalJournal,
            FreshnessState.Current);
}
