using System.Diagnostics;
using System.Text;
using Wolpertinger.Edge.Runtime;

namespace Wolpertinger.Integration.Tests;

public sealed class KernelRecoveryTests
{
    [Fact]
    public async Task KillingActivePromotesCaughtUpShadowWithFreshEpochAndRejoins()
    {
        var root=FsdJumpVerticalSliceTests.RepoRoot(); var data=FsdJumpVerticalSliceTests.TempData(); var kernel=FsdJumpVerticalSliceTests.Kernel(root);
        await using var runner=await VerticalSliceRunner.OpenAsync(data,kernel);
        await BindAsync(runner); var before=runner.KernelDiagnostics;
        Kill(before.ActiveProcessId!.Value);
        await runner.ProcessJournalLineAsync(JumpLine());
        var after=runner.KernelDiagnostics;
        Assert.True(after.Epoch>before.Epoch); Assert.Equal(before.ShadowProcessId,after.ActiveProcessId);
        Assert.NotEqual(before.ActiveProcessId,after.ShadowProcessId); Assert.Equal(2UL,after.LastAgreedCursor!.Value.EvidenceSequence);
        Assert.Single(runner.Outputs);
    }

    [Fact]
    public async Task KillingShadowRebuildsItWithoutChangingAuthorityEpoch()
    {
        var root=FsdJumpVerticalSliceTests.RepoRoot(); var data=FsdJumpVerticalSliceTests.TempData(); var kernel=FsdJumpVerticalSliceTests.Kernel(root);
        await using var runner=await VerticalSliceRunner.OpenAsync(data,kernel);
        await BindAsync(runner); var before=runner.KernelDiagnostics;
        Kill(before.ShadowProcessId!.Value);
        await runner.ProcessJournalLineAsync(JumpLine());
        var after=runner.KernelDiagnostics;
        Assert.Equal(before.Epoch,after.Epoch); Assert.Equal(before.ActiveProcessId,after.ActiveProcessId);
        Assert.NotEqual(before.ShadowProcessId,after.ShadowProcessId); Assert.Equal(2UL,after.LastAgreedCursor!.Value.EvidenceSequence);
        Assert.Single(runner.Outputs);
    }

    private static async Task BindAsync(VerticalSliceRunner r){await r.ProcessJournalLineAsync(Encoding.UTF8.GetBytes("{\"timestamp\":\"2026-09-08T00:00:00Z\",\"event\":\"Fileheader\",\"part\":1,\"gameversion\":\"4.2.2.0\",\"build\":\"r300000/r0\"}")); await r.ProcessJournalLineAsync(Encoding.UTF8.GetBytes("{\"timestamp\":\"2026-09-08T00:00:01Z\",\"event\":\"Commander\",\"FID\":\"F100\"}"));}
    private static ReadOnlyMemory<byte> JumpLine()=>Encoding.UTF8.GetBytes("{\"timestamp\":\"2026-09-08T00:00:02Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Recovery\",\"SystemAddress\":42,\"StarPos\":[1,2,3],\"JumpDist\":10,\"FuelUsed\":2,\"FuelLevel\":20}");
    private static void Kill(int pid){using var p=Process.GetProcessById(pid); p.Kill(entireProcessTree:true); p.WaitForExit(5000); Assert.True(p.HasExited);}
}
