using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Kernel;
using Wolpertinger.Edge.Persistence;

namespace Wolpertinger.Edge.Tests.Kernel;

public sealed class KernelSupervisorTests
{
    [Fact]
    public async Task HealthyFanOutPublishesOnlyFencedActiveResult()
    {
        var active = new FakeKernelClient(Result(KernelRole.Active));
        var shadow = new FakeKernelClient(Result(KernelRole.Shadow));
        var epochs = new FakeEpochStore();
        await using var supervisor = new KernelSupervisor(active, shadow, epochs);

        await supervisor.StartAsync();
        var result = await supervisor.ApplyAsync(Observation());

        Assert.Equal(1UL, supervisor.CurrentEpoch);
        Assert.Equal(KernelRole.Active, result.Role);
        Assert.Equal(1UL, result.Epoch);
        Assert.NotNull(result.JumpFact);
        Assert.Equal(active.AppliedBytes.Single(), shadow.AppliedBytes.Single());
        Assert.Equal((1UL, KernelRole.Active), Assert.Single(active.RoleAssignments));
        Assert.Equal((1UL, KernelRole.Shadow), Assert.Single(shadow.RoleAssignments));
    }
    [Fact]
    public async Task StaleActiveEpochIsRejectedBeforePublish()
    {
        var active = new FakeKernelClient(Result(KernelRole.Active, epoch: 0));
        var shadow = new FakeKernelClient(Result(KernelRole.Shadow));
        await using var supervisor = new KernelSupervisor(active, shadow, new FakeEpochStore());

        await supervisor.StartAsync();

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => supervisor.ApplyAsync(Observation()));
        Assert.Contains("epoch", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongActiveRoleIsRejectedBeforePublish()
    {
        var active = new FakeKernelClient(Result(KernelRole.Shadow));
        var shadow = new FakeKernelClient(Result(KernelRole.Shadow));
        await using var supervisor = new KernelSupervisor(active, shadow, new FakeEpochStore());

        await supervisor.StartAsync();

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => supervisor.ApplyAsync(Observation()));
        Assert.Contains("role", error.Message, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task DigestMismatchRaisesDivergence()
    {
        var active = new FakeKernelClient(Result(KernelRole.Active));
        var mismatch = FixedBytes32.FromHex(new string('f', 64));
        var shadow = new FakeKernelClient(Result(KernelRole.Shadow) with { StateDigest = mismatch });
        await using var supervisor = new KernelSupervisor(active, shadow, new FakeEpochStore());

        await supervisor.StartAsync();

        await Assert.ThrowsAsync<KernelDivergenceException>(
            () => supervisor.ApplyAsync(Observation()));
    }

    [Fact]
    public async Task JumpFactMismatchRaisesDivergenceBeforePublish()
    {
        var activeResult = Result(KernelRole.Active);
        var shadowFact = activeResult.JumpFact! with { StarSystem = "Corrupted Shadow Fact" };
        var shadowResult = Result(KernelRole.Shadow) with { JumpFact = shadowFact };
        await using var supervisor = new KernelSupervisor(
            new FakeKernelClient(activeResult),
            new FakeKernelClient(shadowResult),
            new FakeEpochStore());

        await supervisor.StartAsync();

        var error = await Assert.ThrowsAsync<KernelDivergenceException>(() => supervisor.ApplyAsync(Observation()));
        Assert.Contains("fact", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KernelSupervisorLifecycle.Faulted, supervisor.Diagnostics.Lifecycle);
    }

    [Fact]
    public async Task CursorMismatchRaisesDivergence()
    {
        var active = new FakeKernelClient(Result(KernelRole.Active));
        var shadow = new FakeKernelClient(
            Result(KernelRole.Shadow) with { Cursor = new ObservationCursor(3, 0) });
        await using var supervisor = new KernelSupervisor(active, shadow, new FakeEpochStore());

        await supervisor.StartAsync();

        await Assert.ThrowsAsync<KernelDivergenceException>(
            () => supervisor.ApplyAsync(Observation()));
    }
    [Fact]
    public async Task StartReplaysConfiguredLedgerToAgreementBeforeReturning()
    {
        var active = new FakeKernelClient(SessionResult(KernelRole.Active), Result(KernelRole.Active));
        var shadow = new FakeKernelClient(SessionResult(KernelRole.Shadow), Result(KernelRole.Shadow));
        var replay = new FakeReplaySource(SessionObservation(), Observation());
        await using var supervisor = new KernelSupervisor(
            active, shadow, new FakeEpochStore(), replay, new FakeKernelClientFactory());

        await supervisor.StartAsync();

        Assert.Equal(new ObservationCursor(2, 0), supervisor.Diagnostics.LastAgreedCursor);
        Assert.Equal(Result(KernelRole.Active).StateDigest, supervisor.Diagnostics.LastAgreedDigest);
        Assert.Equal(2, active.AppliedBytes.Count);
        Assert.Equal(2, shadow.AppliedBytes.Count);
    }
    [Fact]
    public async Task AmbiguousApplyFaultsSupervisorLifecycle()
    {
        var active = new FakeKernelClient();
        var shadow = new FakeKernelClient();
        await using var supervisor = new KernelSupervisor(active, shadow, new FakeEpochStore());

        await supervisor.StartAsync();
        Assert.Equal(KernelSupervisorLifecycle.Synchronized, supervisor.Diagnostics.Lifecycle);

        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.ApplyAsync(Observation()));
        Assert.Equal(KernelSupervisorLifecycle.Faulted, supervisor.Diagnostics.Lifecycle);
    }

    [Fact]
    public async Task AgreedNonCommittedKernelStatusFaultsSupervisor()
    {
        var rejectedActive = Result(KernelRole.Active) with { Status = KernelResponseStatus.SequenceGap, JumpFact = null };
        var rejectedShadow = Result(KernelRole.Shadow) with { Status = KernelResponseStatus.SequenceGap, JumpFact = null };
        await using var supervisor = new KernelSupervisor(
            new FakeKernelClient(rejectedActive),
            new FakeKernelClient(rejectedShadow),
            new FakeEpochStore());

        await supervisor.StartAsync();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.ApplyAsync(Observation()));
        Assert.Contains("SequenceGap", error.Message, StringComparison.Ordinal);
        Assert.Equal(KernelSupervisorLifecycle.Faulted, supervisor.Diagnostics.Lifecycle);
    }

    [Fact]
    public async Task DeadActiveIsPromotedBeforeNextDispatchAndEpochAdvances()
    {
        var oldActive = new FakeKernelClient(SessionResult(KernelRole.Active));
        var oldShadow = new FakeKernelClient(SessionResult(KernelRole.Shadow), JumpResult(KernelRole.Active, epoch: 2));
        var replacement = new FakeKernelClient(SessionResult(KernelRole.Shadow, epoch: 2), JumpResult(KernelRole.Shadow, epoch: 2));
        var replay = new FakeReplaySource(SessionObservation());
        var factory = new FakeKernelClientFactory(replacement);
        await using var supervisor = new KernelSupervisor(oldActive, oldShadow, new FakeEpochStore(), replay, factory);

        await supervisor.StartAsync();
        oldActive.IsHealthy = false;

        var result = await supervisor.ApplyAsync(Observation());

        Assert.Equal(2UL, supervisor.CurrentEpoch);
        Assert.Equal(KernelRole.Active, result.Role);
        Assert.Equal((2UL, KernelRole.Active), oldShadow.RoleAssignments.Last());
        Assert.Equal((2UL, KernelRole.Shadow), replacement.RoleAssignments.Last());
        Assert.Equal(2, replacement.AppliedBytes.Count);
    }

    [Fact]
    public async Task DeadShadowRejoinMustAgreeOnSurfacedJumpFact()
    {
        var active = new FakeKernelClient(SessionResult(KernelRole.Active), JumpResult(KernelRole.Active));
        var oldShadow = new FakeKernelClient(SessionResult(KernelRole.Shadow));
        var corruptFact = JumpResult(KernelRole.Shadow) with
        {
            JumpFact = JumpResult(KernelRole.Shadow).JumpFact! with { StarSystem = "Corrupted Rejoin Fact" },
        };
        var replacement = new FakeKernelClient(SessionResult(KernelRole.Shadow), corruptFact);
        var replay = new FakeReplaySource(SessionObservation());
        await using var supervisor = new KernelSupervisor(
            active, oldShadow, new FakeEpochStore(), replay, new FakeKernelClientFactory(replacement));

        await supervisor.StartAsync();
        oldShadow.IsHealthy = false;
        replay.Add(Observation());

        var error = await Assert.ThrowsAsync<KernelDivergenceException>(() => supervisor.ApplyAsync(Observation()));
        Assert.Contains("fact", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KernelSupervisorLifecycle.Faulted, supervisor.Diagnostics.Lifecycle);
    }

    [Fact]
    public async Task DeadShadowRejoinsAtSameEpochAfterActiveProgresses()
    {
        var active = new FakeKernelClient(SessionResult(KernelRole.Active), JumpResult(KernelRole.Active));
        var oldShadow = new FakeKernelClient(SessionResult(KernelRole.Shadow));
        var replacement = new FakeKernelClient(SessionResult(KernelRole.Shadow), JumpResult(KernelRole.Shadow));
        var replay = new FakeReplaySource(SessionObservation());
        var factory = new FakeKernelClientFactory(replacement);
        await using var supervisor = new KernelSupervisor(active, oldShadow, new FakeEpochStore(), replay, factory);

        await supervisor.StartAsync();
        oldShadow.IsHealthy = false;
        replay.Add(Observation());

        var result = await supervisor.ApplyAsync(Observation());

        Assert.Equal(1UL, supervisor.CurrentEpoch);
        Assert.Equal(KernelRole.Active, result.Role);
        Assert.Equal((1UL, KernelRole.Shadow), replacement.RoleAssignments.Last());
        Assert.Equal(2, replacement.AppliedBytes.Count);
    }
    private static ObservationEnvelope SessionObservation()
        => CborContractCodec.DecodeObservation(ReadVector("session-bound.hex"));

    private static KernelApplyResult SessionResult(KernelRole role, ulong epoch = 1)
        => new(
            KernelResponseStatus.Ok,
            epoch,
            role,
            new ObservationCursor(1, 0),
            FixedBytes32.FromHex("1ffb50cc04196362567e3571209cf0fc7a124c98cea1a0b3dd552d1737f222b6"),
            null);

    private static KernelApplyResult JumpResult(KernelRole role, ulong epoch = 1)
        => Result(role, epoch);
    private static ObservationEnvelope Observation()
        => CborContractCodec.DecodeObservation(ReadVector("fsdjump.hex"));

    private static KernelApplyResult Result(KernelRole role, ulong epoch = 1)
        => new(
            KernelResponseStatus.Ok,
            epoch,
            role,
            new ObservationCursor(2, 0),
            FixedBytes32.FromHex("c71b67e5b6a22923473ff6d3426a927b0d6c854fec911256b8c77b4912f897cf"),
            new KernelJumpFact(
                new ObservationCursor(2, 0),
                1_234_567_890_123_456_789,
                "W. Grantler NX-42",
                new GalacticPosition(new Decimal64(12_345, -3), new Decimal64(-6_789, -2), new Decimal64(42, 0)),
                new Decimal64(55_359, -3),
                new Decimal64(4_843_642, -6),
                new Decimal64(27_123, -3),
                SourceProvenance.LocalJournal,
                FreshnessState.Current,
                SourceProvenance.LocalJournal,
                FreshnessState.Current));
    private static byte[] ReadVector(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "contracts", "v1", name);
        return Convert.FromHexString(File.ReadAllText(path).Trim());
    }

    private sealed class FakeKernelClient : IKernelProcessClient
    {
        private readonly Queue<KernelApplyResult> _results;
        public bool IsHealthy { get; set; } = true;
        public int? ProcessId => null;
        public List<(ulong Epoch, KernelRole Role)> RoleAssignments { get; } = [];
        public List<string> AppliedBytes { get; } = [];
        public FakeKernelClient(params KernelApplyResult[] results) => _results = new(results);
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetRoleAsync(ulong epoch, KernelRole role, CancellationToken cancellationToken = default)
        {
            RoleAssignments.Add((epoch, role));
            return Task.CompletedTask;
        }
        public Task<KernelApplyResult> ApplyAsync(ObservationEnvelope observation, CancellationToken cancellationToken = default)
        {
            AppliedBytes.Add(Convert.ToHexString(CborContractCodec.Encode(observation)));
            return Task.FromResult(_results.Dequeue());
        }
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeKernelClientFactory : IKernelProcessClientFactory
    {
        private readonly Queue<IKernelProcessClient> _clients;
        public FakeKernelClientFactory(params IKernelProcessClient[] clients) => _clients = new(clients);
        public IKernelProcessClient Create() => _clients.Dequeue();
    }

    private sealed class FakeReplaySource : IObservationReplaySource
    {
        private readonly List<ObservationEnvelope> _observations;
        public FakeReplaySource(params ObservationEnvelope[] observations) => _observations = [.. observations];
        public void Add(ObservationEnvelope observation) => _observations.Add(observation);
        public async IAsyncEnumerable<ObservationEnvelope> ReadObservationsAsync(
            ObservationCursor? after,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var observation in _observations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (after is null || Compare(observation.Cursor, after.Value) > 0)
                    yield return observation;
            }
            await Task.CompletedTask;
        }
        private static int Compare(ObservationCursor left, ObservationCursor right)
            => left.EvidenceSequence != right.EvidenceSequence
                ? left.EvidenceSequence.CompareTo(right.EvidenceSequence)
                : left.MessageOrdinal.CompareTo(right.MessageOrdinal);
    }
    private sealed class FakeEpochStore : IAuthorityEpochStore
    {
        private ulong _epoch;
        public Task<ulong> NextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(++_epoch);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
