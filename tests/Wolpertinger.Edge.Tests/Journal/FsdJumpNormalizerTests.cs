using System.Text.Json;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Journal;

namespace Wolpertinger.Edge.Tests.Journal;

public sealed class FsdJumpNormalizerTests
{
    [Fact]
    public void NormalizeUsesExactLexicalNumbersIncludingScientificNotation()
    {
        using var json = JsonDocument.Parse("""
            {"timestamp":"2026-09-08T00:00:04Z","event":"FSDJump","StarSystem":"W. Grantler NX-42","SystemAddress":1234567890123456789,"StarPos":[1.2345E1,-6.789E1,42],"JumpDist":5.5359e1,"FuelUsed":4.843642E0,"FuelLevel":2.7123e1,"UnknownFutureField":true}
            """);

        var draft = FsdJumpNormalizer.Normalize(Receipt(), json.RootElement, Binding());
        var payload = Assert.IsType<FsdJumpPayload>(draft.Payload);

        Assert.Equal(new Decimal64(12345, -3), payload.Position.X);
        Assert.Equal(new Decimal64(-6789, -2), payload.Position.Y);
        Assert.Equal(new Decimal64(42, 0), payload.Position.Z);
        Assert.Equal(new Decimal64(55359, -3), payload.JumpDistance);
        Assert.Equal(new Decimal64(4843642, -6), payload.FuelUsed);
        Assert.Equal(new Decimal64(27123, -3), payload.FuelLevel);
        Assert.Equal(1_234_567_890_123_456_789UL, payload.SystemAddress);
        Assert.Equal(SourceProvenance.LocalJournal, draft.Provenance);
    }

    [Fact]
    public void NormalizeRejectsCoefficientOverflowWithoutFloatingPointFallback()
    {
        using var json = JsonDocument.Parse("""
            {"event":"FSDJump","StarSystem":"Test","SystemAddress":1,"StarPos":[0,0,0],"JumpDist":92233720368547758080,"FuelUsed":1,"FuelLevel":10}
            """);

        Assert.Throws<OverflowException>(
            () => FsdJumpNormalizer.Normalize(Receipt(), json.RootElement, Binding()));
    }

    [Fact]
    public void NormalizeRejectsMissingRequiredField()
    {
        using var json = JsonDocument.Parse("""
            {"event":"FSDJump","StarSystem":"Test","SystemAddress":1,"StarPos":[0,0,0],"JumpDist":1,"FuelUsed":1}
            """);

        Assert.Throws<InvalidDataException>(
            () => FsdJumpNormalizer.Normalize(Receipt(), json.RootElement, Binding()));
    }

    private static SessionBinding Binding()
        => new(
            FixedBytes16.FromHex("a0a1a2a3a4a5a6a7a8a9aaabacadaeaf"),
            new ProfileKey("FTEST0001", GalaxyRealm.Live, 0));

    private static RawEvidenceReceipt Receipt()
        => new(
            new EvidenceReference(4, 0, 400, 100),
            FixedBytes32.FromHex("202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f"),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_001_000),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_001_200),
            IsDurable: true);
}
