using System.Text;
using System.Text.Json;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Frontier;
using Wolpertinger.Edge.Journal;
using Wolpertinger.Edge.Kernel;
using Wolpertinger.Edge.Persistence;
using static Wolpertinger.Integration.Tests.FsdJumpVerticalSliceTests;

namespace Wolpertinger.Integration.Tests;

public sealed class CommanderVesselTrustedAcceptanceTests
{
    [Fact]
    public async Task DurableSanitizedProfileFlowsThroughBindingLedgerAndRealKernel()
    {
        var root = RepoRoot();
        var data = TempData();
        var sessionId = FixedBytes16.FromHex("a0a1a2a3a4a5a6a7a8a9aaabacadaeaf");
        var profile = new ProfileKey("FTEST0001", GalaxyRealm.Live, 17);
        var binding = new SessionBinding(sessionId, profile);
        var observed = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_001_000);
        var rawDirectory = Path.Combine(data, "evidence");
        var ledgerPath = Path.Combine(data, "normalized", "observations.bin");
        var fixtureBytes = await File.ReadAllBytesAsync(Path.Combine(root, "fixtures", "capi", "profile-r0-sanitized.json"));

        await using var ledger = await NormalizedObservationLedger.OpenAsync(ledgerPath);
        var sessionPayload = Encoding.UTF8.GetBytes("{\"event\":\"Commander\",\"FID\":\"FTEST0001\"}");
        RawEvidenceReceipt sessionReceipt;
        RawEvidenceReceipt profileReceipt;
        NormalizedObservationCommit boundCommit;
        NormalizedObservationCommit profileCommit;
        await using (var rawEvidence = await SegmentedEvidenceLog.OpenAsync(rawDirectory))
        {
            sessionReceipt = await rawEvidence.AppendAsync(
                new RawEvidenceInput(RawEvidenceSourceKind.LocalJournal, sessionPayload, observed));
            var sessionDraft = new ObservationEnvelopeDraft(
                sessionReceipt.EvidenceDigest,
                sessionId,
                profile,
                ObservationKind.SessionBound,
                observed.ToUnixTimeMilliseconds(),
                observed.ToUnixTimeMilliseconds(),
                observed.AddMilliseconds(1).ToUnixTimeMilliseconds(),
                1,
                SessionBoundPayload.Instance,
                SourceProvenance.LocalJournal);
            boundCommit = await ledger.CommitAsync(sessionReceipt, NormalizationDisposition.Dispatchable, sessionDraft);
            profileReceipt = await rawEvidence.AppendAsync(
                new RawEvidenceInput(RawEvidenceSourceKind.Sample, fixtureBytes, observed.AddSeconds(1)));
            using var profileJson = JsonDocument.Parse(fixtureBytes);
            var profileDraft = FrontierProfileNormalizer.Normalize(profileReceipt, profileJson.RootElement, binding);
            profileCommit = await ledger.CommitAsync(profileReceipt, NormalizationDisposition.Dispatchable, profileDraft);
        }

        Assert.True(sessionReceipt.IsDurable);
        Assert.True(profileReceipt.IsDurable);
        Assert.True(boundCommit.IsDurable);
        Assert.True(profileCommit.IsDurable);
        Assert.NotNull(boundCommit.Observation);
        Assert.NotNull(profileCommit.Observation);
        Assert.Equal(2UL, profileCommit.EvidenceSequence);
        var recovered = await EvidenceLogRecovery.RecoverAsync(rawDirectory);
        Assert.Equal(2, recovered.Count);
        Assert.Equal(RawEvidenceSourceKind.Sample, recovered[1].SourceKind);
        Assert.Equal(fixtureBytes, recovered[1].Payload);

        await using var kernel = new KernelProcessClient(
            new KernelProcessOptions(Kernel(root), TimeSpan.FromSeconds(5)));
        await kernel.StartAsync();
        await kernel.SetRoleAsync(1, KernelRole.Active);
        var boundResult = await kernel.ApplyAsync(boundCommit.Observation);
        Assert.Equal(KernelResponseStatus.Ok, boundResult.Status);
        var accepted = await kernel.ApplyAsync(profileCommit.Observation);

        Assert.Equal(KernelResponseStatus.Ok, accepted.Status);
        Assert.NotNull(accepted.CommanderVesselFact);
        var normalizedPayload = Assert.IsType<CommanderVesselPayload>(profileCommit.Observation.Payload);
        var acceptedFact = accepted.CommanderVesselFact!;
        Assert.Equal(profileCommit.Observation.Cursor, acceptedFact.Cursor);
        Assert.Equal(SourceProvenance.Sample, acceptedFact.Provenance);
        Assert.Equal(FreshnessState.Current, acceptedFact.Freshness);
        Assert.InRange(acceptedFact.CommanderName.Utf8ByteLength, 1, CommanderName.MaximumUtf8Bytes);
        Assert.InRange(acceptedFact.VesselName.Utf8ByteLength, 1, VesselName.MaximumUtf8Bytes);
        Assert.Equal(normalizedPayload.CommanderName, acceptedFact.CommanderName);
        Assert.Equal(normalizedPayload.CommanderAlive, acceptedFact.CommanderAlive);
        Assert.Equal(normalizedPayload.CommanderDocked, acceptedFact.CommanderDocked);
        Assert.Equal(normalizedPayload.CommanderOnFoot, acceptedFact.CommanderOnFoot);
        Assert.Equal(normalizedPayload.VesselName, acceptedFact.VesselName);
        Assert.Equal(normalizedPayload.ShipAlive, acceptedFact.ShipAlive);
        Assert.NotEqual(boundResult.StateDigest, accepted.StateDigest);

        var mismatchedProfile = profileCommit.Observation with
        {
            Cursor = new ObservationCursor(3, 0),
            Profile = new ProfileKey("FOTHER0001", GalaxyRealm.Live, 17),
            EvidenceDigest = FixedBytes32.FromHex("404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f"),
        };
        var rejected = await kernel.ApplyAsync(mismatchedProfile);
        Assert.Equal(KernelResponseStatus.IdentityConflict, rejected.Status);
        Assert.Null(rejected.CommanderVesselFact);
        Assert.Equal(accepted.StateDigest, rejected.StateDigest);

        var replay = await kernel.ApplyAsync(profileCommit.Observation);
        Assert.Equal(KernelResponseStatus.Idempotent, replay.Status);
        Assert.Null(replay.CommanderVesselFact);
        Assert.Equal(accepted.StateDigest, replay.StateDigest);
    }
}
