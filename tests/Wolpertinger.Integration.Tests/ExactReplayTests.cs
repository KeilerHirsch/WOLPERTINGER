using System.Text;
using Wolpertinger.Edge.Replay;
using Wolpertinger.Edge.Runtime;

namespace Wolpertinger.Integration.Tests;

public sealed class ExactReplayTests
{
    [Fact]
    public async Task ReplayNeedsOnlyNormalizedLedgerAndReproducesDigestAndOutputs()
    {
        var root = FsdJumpVerticalSliceTests.RepoRoot();
        var data = FsdJumpVerticalSliceTests.TempData(); var kernel = FsdJumpVerticalSliceTests.Kernel(root);
        IReadOnlyList<Wolpertinger.Edge.Output.CopilotOutput> liveOutputs;
        Wolpertinger.Edge.Contracts.FixedBytes32? liveDigest;
        await using (var live = await VerticalSliceRunner.OpenAsync(data, kernel))
        {
            foreach (var line in File.ReadLines(Path.Combine(root,"fixtures","journal","live-v4-fsdjump-session.jsonl")))
                await live.ProcessJournalLineAsync(Encoding.UTF8.GetBytes(line));
            liveOutputs = live.Outputs.ToArray(); liveDigest = live.FinalStateDigest;
        }

        Directory.Delete(Path.Combine(data, "evidence"), recursive: true);
        File.Delete(Path.Combine(data, "projections.db"));
        var replay = await new ExactReplayRunner().RunAsync(data, kernel);

        Assert.Equal(liveDigest, replay.FinalStateDigest);
        Assert.Equal(liveOutputs, replay.Outputs);
    }
}
