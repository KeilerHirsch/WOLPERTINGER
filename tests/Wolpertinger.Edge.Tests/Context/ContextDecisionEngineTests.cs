using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Tests.TestSupport;

namespace Wolpertinger.Edge.Tests.Context;

public sealed class ContextDecisionEngineTests
{
    [Fact]
    public void AppliedJumpIsDeterministicallyDisplayWorthy()
    {
        var engine = new ContextDecisionEngine();
        var fact = TestFacts.Jump();

        var first = engine.Decide(fact);
        var second = engine.Decide(fact);

        Assert.True(first.Surface);
        Assert.Equal("JumpCompleted", first.ReasonCode);
        Assert.Equal(OutputChannel.Display, first.Channel);
        Assert.Equal(first, second);
    }
}
