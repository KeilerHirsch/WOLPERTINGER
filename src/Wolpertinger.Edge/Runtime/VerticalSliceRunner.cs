using System.Text.Json;
using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Diagnostics;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Journal;
using Wolpertinger.Edge.Kernel;
using Wolpertinger.Edge.Output;
using Wolpertinger.Edge.Persistence;
using Wolpertinger.Edge.Projections;
using Wolpertinger.Edge.Presentation;

namespace Wolpertinger.Edge.Runtime;

public sealed class VerticalSliceRunner : IAsyncDisposable
{
    private readonly SegmentedEvidenceLog _evidence;
    private readonly NormalizedObservationLedger _ledger;
    private readonly KernelSupervisor _supervisor;
    private readonly ProjectionStore _projections;
    private readonly IPresentationPublisher _presentationPublisher;
    private readonly SessionIdentityTracker _identity = new();
    private readonly ContextDecisionEngine _context = new();
    private readonly CopilotOutputFormatter _formatter = new();
    private readonly List<CopilotOutput> _outputs = [];
    private readonly List<DiagnosticEvent> _diagnostics = [];

    private VerticalSliceRunner(SegmentedEvidenceLog evidence, NormalizedObservationLedger ledger,
        KernelSupervisor supervisor, ProjectionStore projections, IPresentationPublisher presentationPublisher)
        => (_evidence, _ledger, _supervisor, _projections, _presentationPublisher) =
            (evidence, ledger, supervisor, projections, presentationPublisher);

    public IReadOnlyList<CopilotOutput> Outputs => _outputs;
    public IReadOnlyList<DiagnosticEvent> Diagnostics => _diagnostics;
    public FixedBytes32? FinalStateDigest => _supervisor.Diagnostics.LastAgreedDigest;
    public KernelSupervisorDiagnostics KernelDiagnostics => _supervisor.Diagnostics;

    public static async Task<VerticalSliceRunner> OpenAsync(string dataDirectory, string kernelExecutable,
        CancellationToken cancellationToken = default, IPresentationPublisher? presentationPublisher = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory); ArgumentException.ThrowIfNullOrWhiteSpace(kernelExecutable);
        Directory.CreateDirectory(dataDirectory);
        var evidence = await SegmentedEvidenceLog.OpenAsync(Path.Combine(dataDirectory, "evidence"), cancellationToken: cancellationToken);
        var ledger = await NormalizedObservationLedger.OpenAsync(Path.Combine(dataDirectory, "normalized", "observations.bin"), cancellationToken);
        var epochs = await AuthorityEpochStore.OpenAsync(Path.Combine(dataDirectory, "control", "authority-epochs.bin"), cancellationToken);
        var supervisor = KernelSupervisor.Create(new KernelProcessOptions(kernelExecutable, TimeSpan.FromSeconds(5)), epochs, ledger);
        await supervisor.StartAsync(cancellationToken);
        var projections = await ProjectionStore.OpenAsync(Path.Combine(dataDirectory, "projections.db"), cancellationToken);
        var runner = new VerticalSliceRunner(evidence, ledger, supervisor, projections,
            presentationPublisher ?? NullPresentationPublisher.Instance);
        await runner.RestoreIdentityFromLedgerAsync(cancellationToken);
        return runner;
    }

    public async Task ProcessJournalLineAsync(ReadOnlyMemory<byte> line, CancellationToken cancellationToken = default)
    {
        if (line.IsEmpty) throw new ArgumentException("Journal line is empty.", nameof(line));
        if (_supervisor.Lifecycle != KernelSupervisorLifecycle.Synchronized)
            throw new InvalidOperationException($"Ingest is unavailable while trusted authority is {_supervisor.Lifecycle}; reopen and replay if faulted.");
        var receipt = await _evidence.AppendAsync(new RawEvidenceInput(RawEvidenceSourceKind.LocalJournal, line, DateTimeOffset.UtcNow), cancellationToken);
        JsonDocument? document = null;
        try { document = JsonDocument.Parse(line); }
        catch (JsonException ex) { await RejectAsync(receipt, "InvalidJson", ex.Message, cancellationToken); return; }
        using (document)
        {
            var root = document.RootElement;
            var kind = JournalEventClassifier.Classify(root);
            SessionIdentityResult identity;
            try { identity = _identity.Observe(receipt, root); }
            catch (InvalidDataException ex) { await RejectAsync(receipt, "IdentityInvalid", ex.Message, cancellationToken); return; }

            if (identity.Status == SessionIdentityStatus.IdentityConflict)
            { await RejectAsync(receipt, "IdentityConflict", "Journal identity conflicts with bound session.", cancellationToken); return; }

            ObservationEnvelopeDraft? draft = identity.Draft;
            if (draft is null && kind == JournalEventKind.FsdJump)
            {
                if (_identity.CurrentBinding is null)
                { await RejectAsync(receipt, "IdentityPending", "FSDJump arrived before authoritative session binding.", cancellationToken); return; }
                try { draft = FsdJumpNormalizer.Normalize(receipt, root, _identity.CurrentBinding); }
                catch (Exception ex) when (ex is InvalidDataException or FormatException or OverflowException)
                { await RejectAsync(receipt, "NormalizationRejected", ex.Message, cancellationToken); return; }
            }

            var disposition = draft is null ? NormalizationDisposition.Ignored : NormalizationDisposition.Dispatchable;
            var commit = await _ledger.CommitAsync(receipt, disposition, draft, cancellationToken);
            if (commit.Observation is not null)
                await DispatchAsync(commit.Observation, receipt.Reference, cancellationToken);
        }
    }

    private async Task DispatchAsync(ObservationEnvelope observation, EvidenceReference reference, CancellationToken ct)
    {
        var result = await _supervisor.ApplyAsync(observation, ct);
        if (result.Status == KernelResponseStatus.Idempotent) return;
        if (result.Status != KernelResponseStatus.Ok)
        { _diagnostics.Add(new DiagnosticEvent("KernelRejected", reference.RawOrdinal, result.Status.ToString())); return; }
        if (observation.Kind != ObservationKind.FsdJump) return;
        var fact = JumpFactFactory.Create(observation, reference, result);
        var decision = _context.Decide(fact);
        if (!decision.Surface) return;
        try
        {
            await _presentationPublisher.PublishJumpAsync(fact, decision, ct);
        }
        catch (Exception ex)
        {
            _diagnostics.Add(new DiagnosticEvent("PresentationPublishFailed", reference.RawOrdinal, ex.Message));
        }
        var output = _formatter.Format(fact, decision);
        await _projections.ApplyAsync(output, ct);
        _outputs.Add(output);
    }

    private async Task RestoreIdentityFromLedgerAsync(CancellationToken cancellationToken)
    {
        SessionBinding? binding = null;
        await foreach (var observation in _ledger.ReadObservationsAsync(null, cancellationToken).ConfigureAwait(false))
        {
            if (observation.Kind == ObservationKind.SessionBound)
                binding = new SessionBinding(observation.SessionId, observation.Profile);
        }
        if (binding is not null)
            _identity.Restore(binding);
    }

    private async Task RejectAsync(RawEvidenceReceipt receipt, string code, string message, CancellationToken ct)
    {
        await _ledger.CommitAsync(receipt, NormalizationDisposition.Rejected, null, ct);
        _diagnostics.Add(new DiagnosticEvent(code, receipt.Reference.RawOrdinal, message));
    }

    public async ValueTask DisposeAsync()
    {
        await _projections.DisposeAsync(); await _supervisor.DisposeAsync();
        await _ledger.DisposeAsync(); await _evidence.DisposeAsync();
    }
}
