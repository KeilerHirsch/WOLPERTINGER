using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Presentation;
using Wolpertinger.Edge.Tests.TestSupport;
using Wolpertinger.Presentation.Contracts;

namespace Wolpertinger.Edge.Tests.Presentation;

public sealed class JumpPresentationProjectorTests
{
    private static readonly ContextDecision Decision = new(true, "JumpCompleted", OutputChannel.Display);

    [Fact]
    public void MapsEveryFieldExactly()
    {
        var fact = TestFacts.Jump() with { Cursor = new ObservationCursor(23, 4) };
        var snapshot = JumpPresentationProjector.Project(fact, Decision, revision: 7);
        Assert.Equal(PresentationProtocol.Version, snapshot.ProtocolVersion);
        Assert.Equal(7UL, snapshot.Revision);
        Assert.Equal(new JumpPresentation(
            new PresentationCursor(23, 4),
            new PresentationProfile(fact.Profile.Fid, PresentationRealm.Live, fact.Profile.SaveEpoch),
            new PresentationEvidenceReference(fact.EvidenceReference.RawOrdinal, fact.EvidenceReference.SegmentNumber,
                fact.EvidenceReference.ByteOffset, fact.EvidenceReference.FrameLength),
            fact.EvidenceDigest.Hex, fact.StateDigest.Hex, fact.SystemAddress, fact.StarSystem,
            fact.Position.X.ToString(), fact.Position.Y.ToString(), fact.Position.Z.ToString(),
            fact.JumpDistance.ToString(), fact.FuelUsed.ToString(), fact.FuelLevel.ToString(),
            PresentationProvenance.LocalJournal, PresentationFreshness.Current,
            PresentationProvenance.LocalJournal, PresentationFreshness.Current, "JumpCompleted"), snapshot.Jump);
    }

    [Theory]
    [InlineData(GalaxyRealm.Unknown, PresentationRealm.Unknown)]
    [InlineData(GalaxyRealm.Live, PresentationRealm.Live)]
    [InlineData(GalaxyRealm.Legacy, PresentationRealm.Legacy)]
    [InlineData(GalaxyRealm.BetaOrPts, PresentationRealm.BetaOrPts)]
    public void MapsRealm(GalaxyRealm source, PresentationRealm expected)
    {
        var fact = TestFacts.Jump();
        Assert.Equal(expected, JumpPresentationProjector.Project(
            fact with { Profile = fact.Profile with { Realm = source } }, Decision, 1).Jump!.Profile.Realm);
    }

    [Theory]
    [InlineData(SourceProvenance.Unknown, PresentationProvenance.Unknown)]
    [InlineData(SourceProvenance.LocalJournal, PresentationProvenance.LocalJournal)]
    [InlineData(SourceProvenance.LocalStatus, PresentationProvenance.LocalStatus)]
    [InlineData(SourceProvenance.FrontierApi, PresentationProvenance.FrontierApi)]
    [InlineData(SourceProvenance.Community, PresentationProvenance.Community)]
    [InlineData(SourceProvenance.UserEntered, PresentationProvenance.UserEntered)]
    public void MapsBothProvenances(SourceProvenance source, PresentationProvenance expected)
    {
        var fact = TestFacts.Jump();
        var location = JumpPresentationProjector.Project(fact with { LocationProvenance = source }, Decision, 1).Jump!;
        var fuel = JumpPresentationProjector.Project(fact with { FuelProvenance = source }, Decision, 1).Jump!;
        Assert.Equal(expected, location.LocationProvenance);
        Assert.Equal(PresentationProvenance.LocalJournal, location.FuelProvenance);
        Assert.Equal(expected, fuel.FuelProvenance);
        Assert.Equal(PresentationProvenance.LocalJournal, fuel.LocationProvenance);
    }

    [Theory]
    [InlineData(FreshnessState.Unknown, PresentationFreshness.Unknown)]
    [InlineData(FreshnessState.Current, PresentationFreshness.Current)]
    [InlineData(FreshnessState.Stale, PresentationFreshness.Stale)]
    [InlineData(FreshnessState.Conflicting, PresentationFreshness.Conflicting)]
    public void MapsBothFreshnessStates(FreshnessState source, PresentationFreshness expected)
    {
        var fact = TestFacts.Jump();
        var location = JumpPresentationProjector.Project(fact with { LocationFreshness = source }, Decision, 1).Jump!;
        var fuel = JumpPresentationProjector.Project(fact with { FuelFreshness = source }, Decision, 1).Jump!;
        Assert.Equal(expected, location.LocationFreshness);
        Assert.Equal(PresentationFreshness.Current, location.FuelFreshness);
        Assert.Equal(expected, fuel.FuelFreshness);
        Assert.Equal(PresentationFreshness.Current, fuel.LocationFreshness);
    }

    [Fact]
    public void RejectsUndefinedEnumValuesInEveryField()
    {
        var fact = TestFacts.Jump();
        var invalid = new[]
        {
            fact with { Profile = fact.Profile with { Realm = (GalaxyRealm)255 } },
            fact with { LocationProvenance = (SourceProvenance)255 },
            fact with { FuelProvenance = (SourceProvenance)255 },
            fact with { LocationFreshness = (FreshnessState)255 },
            fact with { FuelFreshness = (FreshnessState)255 },
        };
        foreach (var item in invalid)
            Assert.Throws<InvalidDataException>(() => JumpPresentationProjector.Project(item, Decision, 1));
    }

    [Theory]
    [InlineData(0L, 0)]
    [InlineData(long.MinValue, -18)]
    [InlineData(long.MaxValue, 18)]
    [InlineData(1200L, -3)]
    public void PreservesDecimal64CanonicalTextForAllNumericFields(long coefficient, int exponent)
    {
        var number = new Decimal64(coefficient, (sbyte)exponent);
        var fact = TestFacts.Jump() with
        {
            Position = new GalacticPosition(number, number, number),
            JumpDistance = number, FuelUsed = number, FuelLevel = number,
        };
        var jump = JumpPresentationProjector.Project(fact, Decision with { ReasonCode = "CustomReason" }, 1).Jump!;
        Assert.All(new[] { jump.PositionX, jump.PositionY, jump.PositionZ, jump.JumpDistance, jump.FuelUsed, jump.FuelLevel },
            value => Assert.Equal(number.ToString(), value));
        Assert.Equal("CustomReason", jump.ReasonCode);
    }
}
