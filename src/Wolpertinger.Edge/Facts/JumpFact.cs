using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;

namespace Wolpertinger.Edge.Facts;

public sealed record JumpFact(
    ObservationCursor Cursor,
    ProfileKey Profile,
    EvidenceReference EvidenceReference,
    FixedBytes32 EvidenceDigest,
    FixedBytes32 StateDigest,
    ulong SystemAddress,
    string StarSystem,
    GalacticPosition Position,
    Decimal64 JumpDistance,
    Decimal64 FuelUsed,
    Decimal64 FuelLevel,
    SourceProvenance LocationProvenance,
    FreshnessState LocationFreshness,
    SourceProvenance FuelProvenance,
    FreshnessState FuelFreshness);
