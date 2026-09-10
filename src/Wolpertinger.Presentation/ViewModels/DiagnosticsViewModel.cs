using Wolpertinger.Presentation.Contracts;
using Wolpertinger.Presentation.State;

namespace Wolpertinger.Presentation.ViewModels;

public sealed record DiagnosticsViewModel(
    int ProtocolVersion,
    ulong Revision,
    PresentationCursor Cursor,
    string Fid,
    string RealmText,
    ulong SaveEpoch,
    PresentationEvidenceReference EvidenceReference,
    string EvidenceDigestHex,
    string StateDigestHex,
    ulong SystemAddress,
    string StarSystem,
    string PositionX,
    string PositionY,
    string PositionZ,
    string JumpDistance,
    string FuelUsed,
    string FuelLevel,
    string LocationProvenanceText,
    string LocationFreshnessText,
    string FuelProvenanceText,
    string FuelFreshnessText,
    string ReasonCode,
    PresentationConnectionState Connection);
