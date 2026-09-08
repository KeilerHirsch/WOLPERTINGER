using Wolpertinger.Edge.Facts;

namespace Wolpertinger.Edge.Context;

public sealed class ContextDecisionEngine
{
    public ContextDecision Decide(JumpFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return new ContextDecision(true, "JumpCompleted", OutputChannel.Display);
    }
}
