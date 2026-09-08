using System.Text;
using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Output;
using Wolpertinger.Edge.Tests.TestSupport;

namespace Wolpertinger.Edge.Tests.Output;

public sealed class CopilotOutputFormatterTests
{
    [Fact]
    public void FormattingIsInvariantAndCarriesTraceability()
    {
        var fact = TestFacts.Jump();
        var decision = new ContextDecision(true, "JumpCompleted", OutputChannel.Display);
        var formatter = new CopilotOutputFormatter();

        var first = formatter.Format(fact, decision);
        var second = formatter.Format(fact, decision);

        Assert.Equal("Jump complete: W. Grantler NX-42 - 55.359 ly, fuel 27.123 t.", first.Text);
        Assert.Equal(Encoding.UTF8.GetBytes(first.Text), Encoding.UTF8.GetBytes(second.Text));
        Assert.Equal(fact.Cursor, first.Cursor);
        Assert.Equal(fact.EvidenceDigest, first.EvidenceDigest);
        Assert.Equal(fact.StateDigest, first.StateDigest);
        Assert.Equal("JumpCompleted", first.ReasonCode);
    }
}
