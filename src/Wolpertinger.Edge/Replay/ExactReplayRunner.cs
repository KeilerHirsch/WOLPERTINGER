using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Kernel;
using Wolpertinger.Edge.Output;
using Wolpertinger.Edge.Persistence;
using Wolpertinger.Edge.Runtime;

namespace Wolpertinger.Edge.Replay;

public sealed record ExactReplayResult(FixedBytes32? FinalStateDigest, IReadOnlyList<CopilotOutput> Outputs);

public sealed class ExactReplayRunner
{
    public async Task<ExactReplayResult> RunAsync(string dataDirectory, string kernelExecutable,
        CancellationToken cancellationToken = default)
    {
        await using var ledger = await NormalizedObservationLedger.OpenAsync(
            Path.Combine(dataDirectory, "normalized", "observations.bin"), cancellationToken);
        var replayControl = Path.Combine(dataDirectory, "replay-control", Guid.NewGuid().ToString("N"), "authority-epochs.bin");
        var epochs = await AuthorityEpochStore.OpenAsync(replayControl, cancellationToken);
        await using var supervisor = KernelSupervisor.Create(new KernelProcessOptions(kernelExecutable, TimeSpan.FromSeconds(5)), epochs, ledger);
        await supervisor.StartAsync(cancellationToken);
        var context = new ContextDecisionEngine(); var formatter = new CopilotOutputFormatter();
        var outputs = new List<CopilotOutput>();

        await foreach (var observation in ledger.ReadObservationsAsync(null, cancellationToken))
        {
            var result = await supervisor.ApplyAsync(observation, cancellationToken);
            if (result.Status == KernelResponseStatus.Idempotent) continue;
            if (result.Status != KernelResponseStatus.Ok)
                throw new InvalidDataException($"Exact replay failed closed with kernel status {result.Status}.");
            if (observation.Kind != ObservationKind.FsdJump) continue;
            var entry = ledger.Entries.Single(e => e.EvidenceSequence == observation.Cursor.EvidenceSequence);
            var fact = JumpFactFactory.Create(observation, entry.EvidenceReference, result);
            var decision = context.Decide(fact);
            if (decision.Surface) outputs.Add(formatter.Format(fact, decision));
        }
        return new ExactReplayResult(supervisor.Diagnostics.LastAgreedDigest, outputs);
    }
}
