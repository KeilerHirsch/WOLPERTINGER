namespace Wolpertinger.Edge.Context;

public enum OutputChannel : byte
{
    Display = 1,
}

public sealed record ContextDecision(
    bool Surface,
    string ReasonCode,
    OutputChannel Channel);
