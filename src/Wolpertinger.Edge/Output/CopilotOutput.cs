using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;

namespace Wolpertinger.Edge.Output;

public sealed record CopilotOutput(
    ObservationCursor Cursor,
    EvidenceReference EvidenceReference,
    FixedBytes32 EvidenceDigest,
    FixedBytes32 StateDigest,
    string ReasonCode,
    string Text);
