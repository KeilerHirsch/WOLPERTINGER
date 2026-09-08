using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;

namespace Wolpertinger.Edge.Output;

public sealed record CopilotOutput(
    ObservationCursor Cursor,
    ProfileKey Profile,
    EvidenceReference EvidenceReference,
    FixedBytes32 EvidenceDigest,
    FixedBytes32 StateDigest,
    ulong SystemAddress,
    string StarSystem,
    Decimal64 JumpDistance,
    Decimal64 FuelUsed,
    Decimal64 FuelLevel,
    string ReasonCode,
    string Text);
