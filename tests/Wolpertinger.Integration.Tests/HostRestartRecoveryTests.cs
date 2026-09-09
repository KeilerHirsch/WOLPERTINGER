using System.Diagnostics;
using System.Text;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Persistence;
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

    [Fact]
    public async Task DurablePendingObservationIsClosedByReopenAndFaultedRunnerRefusesFurtherIngest()
    {
        var root = FsdJumpVerticalSliceTests.RepoRoot();
        var data = FsdJumpVerticalSliceTests.TempData();
        var kernel = FsdJumpVerticalSliceTests.Kernel(root);
        var pendingJump = Jump("2026-09-08T00:00:02Z", "Pending Recovery", 42, 10, 2, 20);
        var laterJump = Jump("2026-09-08T00:00:03Z", "Must Not Append", 43, 11, 1, 19);

        await using (var runner = await VerticalSliceRunner.OpenAsync(data, kernel))
        {
            await BindAsync(runner);
            Kill(runner.KernelDiagnostics.ActiveProcessId!.Value);
            Kill(runner.KernelDiagnostics.ShadowProcessId!.Value);

            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ProcessJournalLineAsync(pendingJump));
            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ProcessJournalLineAsync(laterJump));
        }

        await using (var ledger = await NormalizedObservationLedger.OpenAsync(Path.Combine(data, "normalized", "observations.bin")))
            Assert.Equal(2, ledger.Entries.Count(entry => entry.EvidenceSequence.HasValue));

        await using var recovered = await VerticalSliceRunner.OpenAsync(data, kernel);
        Assert.Equal(new ObservationCursor(2, 0), recovered.KernelDiagnostics.LastAgreedCursor);
        Assert.NotNull(recovered.FinalStateDigest);

        await recovered.ProcessJournalLineAsync(Jump("2026-09-08T00:00:04Z", "After Recovery", 44, 12, 1, 18));
        Assert.Equal(new ObservationCursor(3, 0), Assert.Single(recovered.Outputs).Cursor);
    }

    private static async Task BindAsync(VerticalSliceRunner runner)
    {
        await runner.ProcessJournalLineAsync(Encoding.UTF8.GetBytes("{\"timestamp\":\"2026-09-08T00:00:00Z\",\"event\":\"Fileheader\",\"part\":1,\"gameversion\":\"4.2.2.0\",\"build\":\"r300000/r0\"}"));
        await runner.ProcessJournalLineAsync(Encoding.UTF8.GetBytes("{\"timestamp\":\"2026-09-08T00:00:01Z\",\"event\":\"Commander\",\"FID\":\"F100\"}"));
    }

    private static ReadOnlyMemory<byte> Jump(string timestamp, string system, ulong address, decimal distance, decimal used, decimal level)
        => Encoding.UTF8.GetBytes($"{{\"timestamp\":\"{timestamp}\",\"event\":\"FSDJump\",\"StarSystem\":\"{system}\",\"SystemAddress\":{address},\"StarPos\":[1,2,3],\"JumpDist\":{distance.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"FuelUsed\":{used.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"FuelLevel\":{level.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");

    private static void Kill(int pid)
    {
        using var process = Process.GetProcessById(pid);
        process.Kill(entireProcessTree: true);
        process.WaitForExit(5_000);
        Assert.True(process.HasExited);
    }
}
