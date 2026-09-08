using System.Text;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Kernel;
using Wolpertinger.Edge.Persistence;
using Wolpertinger.Edge.Runtime;

namespace Wolpertinger.Integration.Tests;

public sealed class FailureSemanticsTests
{
    [Fact]
    public async Task InvalidNumericIsRetainedButDoesNotAdvanceAuthoritativeState()
    {
        var root=FsdJumpVerticalSliceTests.RepoRoot(); var data=FsdJumpVerticalSliceTests.TempData(); var kernel=FsdJumpVerticalSliceTests.Kernel(root);
        FixedBytes32? boundDigest;
        await using (var runner=await VerticalSliceRunner.OpenAsync(data,kernel))
        {
            await runner.ProcessJournalLineAsync(B("{\"timestamp\":\"2026-09-08T00:00:00Z\",\"event\":\"Fileheader\",\"part\":1,\"gameversion\":\"4.2.2.0\",\"build\":\"r300000/r0\"}"));
            await runner.ProcessJournalLineAsync(B("{\"timestamp\":\"2026-09-08T00:00:01Z\",\"event\":\"Commander\",\"FID\":\"F100\"}"));
            boundDigest=runner.FinalStateDigest;
            await runner.ProcessJournalLineAsync(B("{\"timestamp\":\"2026-09-08T00:00:02Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Bad\",\"SystemAddress\":1,\"StarPos\":[0,0,0],\"JumpDist\":1,\"FuelUsed\":1,\"FuelLevel\":9223372036854775808}"));
            Assert.Equal(boundDigest,runner.FinalStateDigest); Assert.Empty(runner.Outputs);
            Assert.Contains(runner.Diagnostics,x=>x.Code=="NormalizationRejected");
        }
        await using (var validation=await SegmentedEvidenceLog.OpenAsync(Path.Combine(data,"evidence"))) { }
        await using var ledger=await NormalizedObservationLedger.OpenAsync(Path.Combine(data,"normalized","observations.bin"));
        Assert.Equal(3,ledger.Entries.Count); Assert.Equal(1,ledger.Entries.Count(x=>x.EvidenceSequence.HasValue));
        Assert.Equal(NormalizationDisposition.Rejected,ledger.Entries[^1].Disposition);
    }

    [Fact]
    public async Task RealKernelIdentityConflictDoesNotMutateState()
    {
        await using var client=await ActiveClient(); var bound=await client.ApplyAsync(SessionBound("F100",1));
        var conflict=await client.ApplyAsync(Jump("F200",2,Digest('2')));
        Assert.Equal(KernelResponseStatus.IdentityConflict,conflict.Status); Assert.Null(conflict.JumpFact);
        Assert.Equal(bound.StateDigest,conflict.StateDigest);
    }

    [Fact]
    public async Task RealKernelSequenceGapAndIntegrityFaultDoNotMutateState()
    {
        await using var client=await ActiveClient(); await client.ApplyAsync(SessionBound("F100",1));
        var gap=await client.ApplyAsync(Jump("F100",3,Digest('3'))); var afterBound=gap.StateDigest;
        Assert.Equal(KernelResponseStatus.SequenceGap,gap.Status);
        var accepted=await client.ApplyAsync(Jump("F100",2,Digest('2'))); Assert.Equal(KernelResponseStatus.Ok,accepted.Status);
        var fault=await client.ApplyAsync(Jump("F100",2,Digest('9')));
        Assert.Equal(KernelResponseStatus.IntegrityFault,fault.Status); Assert.Equal(accepted.StateDigest,fault.StateDigest);
        Assert.NotEqual(afterBound,accepted.StateDigest);
    }

    private static byte[] B(string s)=>Encoding.UTF8.GetBytes(s);
    private static FixedBytes32 Digest(char c)=>FixedBytes32.FromHex(new string(c,64));
    private static async Task<KernelProcessClient> ActiveClient(){var c=new KernelProcessClient(new KernelProcessOptions(FsdJumpVerticalSliceTests.Kernel(FsdJumpVerticalSliceTests.RepoRoot()),TimeSpan.FromSeconds(5))); await c.StartAsync(); await c.SetRoleAsync(1,KernelRole.Active); return c;}
    private static ObservationEnvelope SessionBound(string fid,ulong seq)=>new(new(seq,0),Digest('1'),FixedBytes16.FromHex(new string('a',32)),new(fid,GalaxyRealm.Live,0),ObservationKind.SessionBound,null,1,1,1,SessionBoundPayload.Instance,SourceProvenance.LocalJournal);
    private static ObservationEnvelope Jump(string fid,ulong seq,FixedBytes32 digest)=>new(new(seq,0),digest,FixedBytes16.FromHex(new string('a',32)),new(fid,GalaxyRealm.Live,0),ObservationKind.FsdJump,null,1,1,1,new FsdJumpPayload("Test",1,new(new(0,0),new(0,0),new(0,0)),new(1,0),new(1,0),new(9,0)),SourceProvenance.LocalJournal);
}
