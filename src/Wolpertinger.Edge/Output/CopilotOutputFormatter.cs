using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Facts;

namespace Wolpertinger.Edge.Output;

public sealed class CopilotOutputFormatter
{
    public CopilotOutput Format(JumpFact fact, ContextDecision decision)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.Surface || decision.Channel != OutputChannel.Display)
            throw new InvalidOperationException("Only surfaced display decisions can produce v0 output.");

        var text = $"Jump complete: {fact.StarSystem} - {fact.JumpDistance} ly, fuel {fact.FuelLevel} t.";
        return new CopilotOutput(
            fact.Cursor,
            fact.Profile,
            fact.EvidenceReference,
            fact.EvidenceDigest,
            fact.StateDigest,
            fact.SystemAddress,
            fact.StarSystem,
            fact.JumpDistance,
            fact.FuelUsed,
            fact.FuelLevel,
            decision.ReasonCode,
            text);
    }
}
