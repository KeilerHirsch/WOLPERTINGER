using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Journal;

namespace Wolpertinger.Edge.Tests.Contracts;

public sealed class R0ContractVersionTests
{
    [Fact]
    public void ProtocolVersionsAndExtendedEnumsAreFrozen()
    {
        Assert.Equal(1, KernelProtocol.Stage1Version);
        Assert.Equal(2, KernelProtocol.R0Version);
        Assert.Equal(KernelProtocol.Stage1Version, KernelProtocol.Version);
        Assert.Equal(3, (byte)ObservationKind.CommanderVessel);
        Assert.Equal(6, (byte)SourceProvenance.Sample);
    }

    [Fact]
    public void EnvelopeAndDraftDefaultToStage1AndDraftPropagatesExplicitVersion()
    {
        var draft = CreateDraft();
        Assert.Equal(KernelProtocol.Stage1Version, draft.ProtocolVersion);
        Assert.Equal(KernelProtocol.Stage1Version, draft.ToEnvelope(9).ProtocolVersion);

        var r0 = draft with { ProtocolVersion = KernelProtocol.R0Version };
        Assert.Equal(KernelProtocol.R0Version, r0.ToEnvelope(9).ProtocolVersion);
    }

    [Fact]
    public void R0SampleFsdJumpRoundTripsWithProtocolVersionTwo()
    {
        var observation = CreateDraft() with
        {
            ProtocolVersion = KernelProtocol.R0Version,
            Kind = ObservationKind.FsdJump,
            Payload = CreateJump(),
            Provenance = SourceProvenance.Sample,
        };
        var envelope = observation.ToEnvelope(10);

        var encoded = CborContractCodec.Encode(envelope);
        var decoded = CborContractCodec.DecodeObservation(encoded);

        Assert.Equal(KernelProtocol.R0Version, decoded.ProtocolVersion);
        Assert.Equal(SourceProvenance.Sample, decoded.Provenance);
        Assert.Equal(ObservationKind.FsdJump, decoded.Kind);
    }

    [Fact]
    public void Stage1RejectsSampleAndUnknownProtocolVersion()
    {
        var sample = CreateDraft() with { Provenance = SourceProvenance.Sample };
        Assert.Throws<InvalidDataException>(() => CborContractCodec.Encode(sample.ToEnvelope(11)));

        var unknown = CreateDraft() with { ProtocolVersion = 3 };
        Assert.Throws<InvalidDataException>(() => CborContractCodec.Encode(unknown.ToEnvelope(12)));
    }

    [Fact]
    public void Stage1RejectsCommanderVesselKind()
    {
        var commanderVessel = CreateDraft() with
        {
            Kind = ObservationKind.CommanderVessel,
            Payload = CreateJump(),
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => CborContractCodec.Encode(commanderVessel.ToEnvelope(13)));
        Assert.Contains("Unknown observation kind for protocol version.", exception.Message);
    }

    private static ObservationEnvelopeDraft CreateDraft()
        => new(
            FixedBytes32.FromHex("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F"),
            FixedBytes16.FromHex("A0A1A2A3A4A5A6A7A8A9AAABACADAEAF"),
            new ProfileKey("FTEST0001", GalaxyRealm.Live, 0),
            ObservationKind.SessionBound,
            1_700_000_000_000,
            1_700_000_000_100,
            1_700_000_000_200,
            1,
            SessionBoundPayload.Instance,
            SourceProvenance.LocalJournal);

    private static FsdJumpPayload CreateJump()
        => new(
            "W. Grantler NX-42",
            1_234_567_890_123_456_789UL,
            new GalacticPosition(new Decimal64(12345, -3), new Decimal64(-6789, -2), new Decimal64(42, 0)),
            new Decimal64(55359, -3),
            new Decimal64(4843642, -6),
            new Decimal64(27123, -3));
}
