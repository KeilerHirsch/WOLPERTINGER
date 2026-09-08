using System.Text;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Runtime;

namespace Wolpertinger.Integration.Tests;

public sealed class HostRestartRecoveryTests
{
    [Fact]
    public async Task RestartReplaysLedgerRestoresIdentityAndContinuesAtNextCursor()
    {
        var root = FsdJumpVerticalSliceTests.RepoRoot();
        var data = FsdJumpVerticalSliceTests.TempData();
        var kernel = FsdJumpVerticalSliceTests.Kernel(root);
        FixedBytes32? beforeDigest;

        await using (var first = await VerticalSliceRunner.OpenAsync(data, kernel))
        {
            foreach (var line in File.ReadLines(Path.Combine(root, "fixtures", "journal", "live-v4-fsdjump-session.jsonl")))
                await first.ProcessJournalLineAsync(Encoding.UTF8.GetBytes(line));
            beforeDigest = first.FinalStateDigest;
            Assert.Equal(new ObservationCursor(2, 0), first.KernelDiagnostics.LastAgreedCursor);
        }

        await using var restarted = await VerticalSliceRunner.OpenAsync(data, kernel);
        Assert.Equal(new ObservationCursor(2, 0), restarted.KernelDiagnostics.LastAgreedCursor);
        Assert.Equal(beforeDigest, restarted.FinalStateDigest);
        var nextJump = Encoding.UTF8.GetBytes(
            "{\"timestamp\":\"2026-09-08T00:00:05Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Dryio Flyuae AA-A h1\",\"SystemAddress\":1234567890123456790,\"StarPos\":[13.345,-66.89,43],\"JumpDist\":12.5,\"FuelUsed\":1.25,\"FuelLevel\":25.873,\"BoostUsed\":0}");

        await restarted.ProcessJournalLineAsync(nextJump);

        var output = Assert.Single(restarted.Outputs);
        Assert.Equal(new ObservationCursor(3, 0), output.Cursor);
        Assert.Equal("Dryio Flyuae AA-A h1", output.StarSystem);
        Assert.NotEqual(beforeDigest, restarted.FinalStateDigest);
    }
}
