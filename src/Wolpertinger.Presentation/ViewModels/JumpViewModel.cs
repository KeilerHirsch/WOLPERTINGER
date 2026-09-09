using Wolpertinger.Presentation.Contracts;
using Wolpertinger.Presentation.State;

namespace Wolpertinger.Presentation.ViewModels;

public sealed record JumpViewModel(
    int ProtocolVersion,
    ulong Revision,
    PresentationCursor Cursor,
    string Fid,
    string RealmText,
    ulong SaveEpoch,
    ulong SystemAddress,
    string StarSystem,
    string PositionX,
    string PositionY,
    string PositionZ,
    string JumpDistanceText,
    string FuelUsedText,
    string FuelLevelText,
    string LocationProvenanceText,
    string LocationStatusText,
    string FuelProvenanceText,
    string FuelStatusText,
    PresentationEvidenceReference EvidenceReference,
    string EvidenceDigestHex,
    string StateDigestHex,
    string ReasonCode,
    PresentationConnectionState Connection,
    bool IsLive,
    string AvailabilityText,
    DensityPreset Density);
