# WOLPERTINGER Presentation Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first retained Windows presentation slice from the real Stage-1 `JumpFact` through one versioned Presentation Contract into a tray-controlled passive overlay and Fullscreen Hub, while keeping trusted state authoritative and presentation restartable.

**Architecture:** Add a small read-side contract assembly, project trusted Stage-1 facts into complete immutable presentation snapshots, and stream those snapshots over a bounded current-user local pipe to a separate Avalonia presentation process. Surface-neutral state/ViewModels remain platform-free; Windows-only placement, DPI, monitor recovery, HWND styles, click-through, no-activate, and topmost behavior live in one Windows adapter assembly.

**Tech Stack:** .NET 10.0.111, C# 14, Avalonia 12.1.0, `System.IO.Pipes`, `System.Text.Json`, xUnit 2.9.3, existing Ada/SPARK kernel and Stage-1 integration harness.

**Spec:** `docs/superpowers/specs/2026-09-09-wolpertinger-presentation-subsystem-design.md`

## Global Constraints

- Stage 1 remains CLOSED; do not modify trusted-kernel semantics unless a concrete regression proves the presentation work exposed a Stage-1 defect.
- **ONE CORE — MULTIPLE PRESENTATION SURFACES.** No presentation surface owns gameplay truth.
- Release 1 is Windows-first, cross-platform-ready; no Linux/Wayland/X11 implementation in this plan.
- `Wolpertinger.Presentation.Contracts` and `Wolpertinger.Presentation` must contain no HWND, Win32/PInvoke, physical-monitor handles, or Windows-specific window semantics.
- Presentation is a separate process from the authoritative Edge/SPARK path; killing it must not stop trusted processing.
- Presentation consumes `JumpFact` plus deterministic `ContextDecision`; it never reparses Frontier journal JSON.
- Use complete immutable snapshots for the first slice; do not invent a delta protocol or a second persistence store.
- Local transport is `System.IO.Pipes` with `PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous`, byte mode, a 4-byte big-endian length prefix, and a hard 65,536-byte frame limit.
- Wire serialization is UTF-8 `System.Text.Json`; presentation protocol version starts at `1` and is independent of the SPARK CBOR/CDDL protocol.
- Avalonia is pinned to `12.1.0` for this slice; no commercial Avalonia components are required.
- Normal UX order remains good defaults > presets > bounded options > nerd basement.
- No voice, CAPI, EDDN, plugin expansion, broad feature modules, blank-canvas widgets, or full Concept UI in this plan.
- No new general FuE. Research only a concrete blocker and stop as soon as it is answered.

## Planned File Structure

### Shared read-side contract
- `src/Wolpertinger.Presentation.Contracts/Wolpertinger.Presentation.Contracts.csproj` — platform-free presentation DTOs and framing codec.
- `src/Wolpertinger.Presentation.Contracts/PresentationProtocol.cs` — protocol version, pipe default name, and frame bound.
- `src/Wolpertinger.Presentation.Contracts/PresentationSnapshot.cs` — immutable snapshot/fact/profile/provenance/freshness records.
- `src/Wolpertinger.Presentation.Contracts/PresentationSnapshotValidator.cs` — contract-level fail-closed validation.
- `src/Wolpertinger.Presentation.Contracts/PresentationFrameCodec.cs` — bounded big-endian length framing plus JSON serialization.

### Edge projection and server
- `src/Wolpertinger.Edge/Presentation/IPresentationPublisher.cs` — non-authoritative publish seam.
- `src/Wolpertinger.Edge/Presentation/JumpPresentationProjector.cs` — exact `JumpFact`/`ContextDecision` → snapshot mapping.
- `src/Wolpertinger.Edge/Presentation/PresentationStatePublisher.cs` — current full snapshot + bounded latest-update channel.
- `src/Wolpertinger.Edge/Presentation/NullPresentationPublisher.cs` — default no-op so existing Stage-1 callers remain unchanged.
- `src/Wolpertinger.Edge/Presentation/PresentationPipeServer.cs` — current-user local read-only snapshot server.
- Modify `src/Wolpertinger.Edge/Runtime/VerticalSliceRunner.cs` — inject publisher and publish only after trusted success.

### Surface-neutral presentation runtime
- `src/Wolpertinger.Presentation/Wolpertinger.Presentation.csproj` — platform-neutral client/state/ViewModels.
- `src/Wolpertinger.Presentation/Transport/PresentationPipeClient.cs` — reconnectable read-only pipe client.
- `src/Wolpertinger.Presentation/State/PresentationStore.cs` — monotonic snapshot application and connection state.
- `src/Wolpertinger.Presentation/ViewModels/JumpViewModel.cs` — commander-facing immutable display model.
- `src/Wolpertinger.Presentation/ViewModels/DiagnosticsViewModel.cs` — engineering projection of the same snapshot.
- `src/Wolpertinger.Presentation/ViewModels/PresentationViewModelFactory.cs` — deterministic formatting/grammar.
- `src/Wolpertinger.Presentation/Preferences/PresentationPreferences.cs` — bounded surface/density preferences only.

### Windows adapter
- `src/Wolpertinger.Presentation.Windows/Wolpertinger.Presentation.Windows.csproj` — all Windows/Avalonia-native surface behavior.
- `src/Wolpertinger.Presentation.Windows/Displays/DisplayDescriptor.cs` — adapter-local display snapshot.
- `src/Wolpertinger.Presentation.Windows/Displays/DisplayTopologyPolicy.cs` — preferred/game/primary selection and no-teleport recovery.
- `src/Wolpertinger.Presentation.Windows/Displays/DockPlacementPolicy.cs` — DPI-aware dock geometry and clamping.
- `src/Wolpertinger.Presentation.Windows/Interop/WindowsNativeMethods.cs` — isolated P/Invoke declarations/constants.
- `src/Wolpertinger.Presentation.Windows/WindowsOverlayAdapter.cs` — applies no-activate/click-through/topmost and placement.
- `src/Wolpertinger.Presentation.Windows/WindowsHubAdapter.cs` — fullscreen target/recovery behavior.

### Avalonia shell
- `src/Wolpertinger.Presentation.App/Wolpertinger.Presentation.App.csproj` — Windows Release-1 presentation executable.
- `src/Wolpertinger.Presentation.App/Program.cs`, `App.axaml`, `App.axaml.cs` — desktop lifetime and tray control plane.
- `src/Wolpertinger.Presentation.App/PresentationApplicationController.cs` — connection lifecycle, store updates, surface show/hide.
- `src/Wolpertinger.Presentation.App/Views/OverlayWindow.axaml(.cs)` — passive commander overlay.
- `src/Wolpertinger.Presentation.App/Views/FullscreenHubWindow.axaml(.cs)` — larger view over the same jump model.
- `src/Wolpertinger.Presentation.App/Views/DiagnosticsWindow.axaml(.cs)` — first read-only engineering view.
- `src/Wolpertinger.Presentation.App/Preferences/PresentationPreferencesStore.cs` — safe JSON persistence under LocalApplicationData.
- `src/Wolpertinger.Presentation.App/Assets/wolpertinger.ico` — minimal retained tray icon for this slice, not final brand art.

### Tests and docs
- `tests/Wolpertinger.Presentation.Tests/` — contract, codec, store/ViewModel, preference, topology, and architecture-boundary tests.
- Modify `tests/Wolpertinger.Edge.Tests/` — projection/publisher/server tests.
- Modify `tests/Wolpertinger.Integration.Tests/` — real-kernel presentation process-isolation/restart acceptance test.
- Modify `WOLPERTINGER.slnx`, `Directory.Packages.props`, `README.md`, and `ROADMAP.md` only as specified below.

---
### Task 1: Freeze the versioned Presentation Contract

**Files:**
- Create: `src/Wolpertinger.Presentation.Contracts/Wolpertinger.Presentation.Contracts.csproj`
- Create: `src/Wolpertinger.Presentation.Contracts/PresentationProtocol.cs`
- Create: `src/Wolpertinger.Presentation.Contracts/PresentationSnapshot.cs`
- Create: `src/Wolpertinger.Presentation.Contracts/PresentationSnapshotValidator.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj`
- Create: `tests/Wolpertinger.Presentation.Tests/Contracts/PresentationSnapshotTests.cs`
- Modify: `WOLPERTINGER.slnx`

**Interfaces:**
- Consumes: no new product code; this task defines the downstream contract from the approved design.
- Produces: `PresentationProtocol`, `PresentationSnapshot`, `JumpPresentation`, `PresentationCursor`, `PresentationProfile`, `PresentationEvidenceReference`, `PresentationProvenance`, `PresentationFreshness`, and `PresentationSnapshotValidator.Validate(PresentationSnapshot)`.

- [ ] **Step 1: Add the failing contract tests and project references**

Create the test project with the existing centrally managed xUnit packages and a project reference to the new Contracts project. Add tests that require protocol version `1`, frame bound `65_536`, an empty initial snapshot at revision `0`, and rejection of invalid digests/mandatory jump identifiers.

```csharp
[Fact]
public void ValidJumpSnapshotPassesValidation()
{
    var snapshot = TestSnapshots.Jump(revision: 1);
    PresentationSnapshotValidator.Validate(snapshot);
}

[Fact]
public void JumpWithNonHexStateDigestFailsClosed()
{
    var snapshot = TestSnapshots.Jump(revision: 1) with
    {
        Jump = TestSnapshots.Jump(revision: 1).Jump! with { StateDigestHex = "NOPE" }
    };
    Assert.Throws<InvalidDataException>(() => PresentationSnapshotValidator.Validate(snapshot));
}
```

- [ ] **Step 2: Run the contract tests and verify RED**

Run:
```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter FullyQualifiedName~Contracts
```
Expected: build/test failure because the contract types and validator do not exist yet.

- [ ] **Step 3: Implement the minimal immutable contract**

Use exact neutral enum values; do not reference `Wolpertinger.Edge` from the contract assembly.

```csharp
public static class PresentationProtocol
{
    public const int Version = 1;
    public const int MaximumFrameBytes = 65_536;
    public const string DefaultPipeName = "wolpertinger.presentation.v1";
}

public enum PresentationRealm : byte { Unknown = 0, Live = 1, Legacy = 2, BetaOrPts = 3 }
public enum PresentationProvenance : byte { Unknown = 0, LocalJournal = 1, LocalStatus = 2, FrontierApi = 3, Community = 4, UserEntered = 5 }
public enum PresentationFreshness : byte { Unknown = 0, Current = 1, Stale = 2, Conflicting = 3 }

public readonly record struct PresentationCursor(ulong EvidenceSequence, uint MessageOrdinal);
public sealed record PresentationProfile(string Fid, PresentationRealm Realm, ulong SaveEpoch);
public readonly record struct PresentationEvidenceReference(ulong RawOrdinal, uint SegmentNumber, long ByteOffset, int FrameLength);
```
```csharp
public sealed record JumpPresentation(
    PresentationCursor Cursor,
    PresentationProfile Profile,
    PresentationEvidenceReference EvidenceReference,
    string EvidenceDigestHex,
    string StateDigestHex,
    ulong SystemAddress,
    string StarSystem,
    string PositionX,
    string PositionY,
    string PositionZ,
    string JumpDistance,
    string FuelUsed,
    string FuelLevel,
    PresentationProvenance LocationProvenance,
    PresentationFreshness LocationFreshness,
    PresentationProvenance FuelProvenance,
    PresentationFreshness FuelFreshness,
    string ReasonCode);

public sealed record PresentationSnapshot(int ProtocolVersion, ulong Revision, JumpPresentation? Jump)
{
    public static PresentationSnapshot Empty { get; } = new(PresentationProtocol.Version, 0, null);
}
```

`PresentationSnapshotValidator.Validate` must reject unsupported protocol versions, `Jump != null` with revision `0`, blank FID/system/reason code, non-64-character hexadecimal evidence/state digests, negative evidence offsets/frame lengths, and blank canonical numeric strings. It must allow `Unknown`, `Stale`, and `Conflicting` enum values without inventing replacements.

- [ ] **Step 4: Run the contract tests and verify GREEN**

Run the same filtered test command. Expected: PASS.

- [ ] **Step 5: Commit Task 1**

```powershell
git add WOLPERTINGER.slnx src/Wolpertinger.Presentation.Contracts tests/Wolpertinger.Presentation.Tests
git commit -m "feat: add presentation contract"
```

### Task 2: Project trusted JumpFacts into a non-authoritative current snapshot

**Files:**
- Create: `src/Wolpertinger.Edge/Presentation/IPresentationPublisher.cs`
- Create: `src/Wolpertinger.Edge/Presentation/JumpPresentationProjector.cs`
- Create: `src/Wolpertinger.Edge/Presentation/PresentationStatePublisher.cs`
- Create: `src/Wolpertinger.Edge/Presentation/NullPresentationPublisher.cs`
- Modify: `src/Wolpertinger.Edge/Wolpertinger.Edge.csproj`
- Modify: `src/Wolpertinger.Edge/Runtime/VerticalSliceRunner.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Presentation/JumpPresentationProjectorTests.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Presentation/PresentationStatePublisherTests.cs`
- Modify: `tests/Wolpertinger.Edge.Tests/TestSupport/TestFacts.cs` only if a reusable real-shaped `JumpFact` fixture reduces duplication.

**Interfaces:**
- Consumes: Task-1 `PresentationSnapshot` contract plus existing `JumpFact` and `ContextDecision`.
- Produces: `JumpPresentationProjector.Project(JumpFact, ContextDecision, ulong)`, `IPresentationPublisher.PublishJumpAsync(...)`, `PresentationStatePublisher.Current`, and `PresentationStatePublisher.ReadUpdatesAsync(...)`.

- [ ] **Step 1: Write failing exact-mapping tests**

Require an exhaustive mapping from existing Edge enums to presentation enums and canonical numeric text from the existing `Decimal64.ToString()` implementation.

```csharp
var snapshot = JumpPresentationProjector.Project(fact, new ContextDecision(true, "JumpCompleted", OutputChannel.Display), revision: 7);
Assert.Equal(7UL, snapshot.Revision);
Assert.Equal(fact.Cursor.EvidenceSequence, snapshot.Jump!.Cursor.EvidenceSequence);
Assert.Equal(fact.Profile.Fid, snapshot.Jump.Profile.Fid);
Assert.Equal(fact.StateDigest.Hex, snapshot.Jump.StateDigestHex);
Assert.Equal(fact.JumpDistance.ToString(), snapshot.Jump.JumpDistance);
Assert.Equal(PresentationFreshness.Current, snapshot.Jump.LocationFreshness);
Assert.Equal("JumpCompleted", snapshot.Jump.ReasonCode);
```

Also test that publisher revision starts at `0`, increments once per published jump, stores the latest complete snapshot, and collapses a burst to the latest pending update rather than blocking trusted processing.

- [ ] **Step 2: Run the presentation Edge tests and verify RED**

```powershell
dotnet test tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj -c Release --filter FullyQualifiedName~Presentation
```
Expected: FAIL because projector/publisher types are absent.

- [ ] **Step 3: Implement the projector and publisher**

Use an explicit switch for every existing `GalaxyRealm`, `SourceProvenance`, and `FreshnessState` member. Unknown maps to Unknown; an undefined numeric enum value throws `InvalidDataException` instead of being guessed.

```csharp
public interface IPresentationPublisher
{
    PresentationSnapshot Current { get; }
    ValueTask PublishJumpAsync(JumpFact fact, ContextDecision decision, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PresentationSnapshot> ReadUpdatesAsync(CancellationToken cancellationToken = default);
}
```

`PresentationStatePublisher` uses a bounded `Channel<PresentationSnapshot>` with capacity `1`, `FullMode = DropOldest`, `SingleReader = true`, and `SingleWriter = false`. The full latest snapshot remains in `Current`, so dropping intermediate redraw work never loses authoritative truth.

`NullPresentationPublisher` returns `PresentationSnapshot.Empty`, performs no work, and yields no updates.

- [ ] **Step 4: Integrate publishing after trusted success without changing Stage-1 semantics**

Extend `VerticalSliceRunner.OpenAsync` with an optional `IPresentationPublisher? presentationPublisher = null`, store `presentationPublisher ?? NullPresentationPublisher.Instance`, and publish only after `ContextDecision.Surface == true` and after the trusted kernel returned `Ok`.

```csharp
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
```

The catch is intentional failure containment: a broken presentation publisher may produce a diagnostic but may not roll back the trusted transition or suppress the existing Stage-1 `CopilotOutput`.

- [ ] **Step 5: Run Task-2 tests plus the existing Stage-1 Edge suite**

```powershell
dotnet test tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj -c Release
```
Expected: 70 existing Stage-1 tests plus the new presentation tests all PASS; no existing assertion changes merely to accommodate presentation.

- [ ] **Step 6: Commit Task 2**

```powershell
git add src/Wolpertinger.Edge tests/Wolpertinger.Edge.Tests
git commit -m "feat: project trusted facts for presentation"
```

### Task 3: Add bounded current-user pipe transport with full-snapshot reconnect

**Files:**
- Create: `src/Wolpertinger.Presentation.Contracts/PresentationFrameCodec.cs`
- Create: `src/Wolpertinger.Edge/Presentation/PresentationPipeServer.cs`
- Create: `src/Wolpertinger.Presentation/Wolpertinger.Presentation.csproj`
- Create: `src/Wolpertinger.Presentation/Transport/PresentationPipeClient.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/Contracts/PresentationFrameCodecTests.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/Transport/PresentationPipeClientTests.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Presentation/PresentationPipeServerTests.cs`
- Modify: `WOLPERTINGER.slnx`
- Modify: `tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj`

**Interfaces:**
- Consumes: Task-2 `IPresentationPublisher.Current` and `ReadUpdatesAsync`.
- Produces: `PresentationFrameCodec.WriteAsync/ReadAsync`, `PresentationPipeServer.RunAsync`, and `PresentationPipeClient.ReadSnapshotsAsync`.

- [ ] **Step 1: Write failing frame-codec tests**

Test round-trip, partial-stream reads, `0` length, negative/oversized big-endian lengths, truncated JSON, unsupported protocol version, and valid `PresentationSnapshot.Empty`.

```csharp
await using var stream = new MemoryStream();
await PresentationFrameCodec.WriteAsync(stream, TestSnapshots.Jump(1));
stream.Position = 0;
var roundTrip = await PresentationFrameCodec.ReadAsync(stream);
Assert.Equal(TestSnapshots.Jump(1), roundTrip);
```

For the oversized test, write the four bytes representing `PresentationProtocol.MaximumFrameBytes + 1` and assert `InvalidDataException` before allocating a payload buffer.

- [ ] **Step 2: Run the codec tests and verify RED**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter FullyQualifiedName~PresentationFrameCodec
```
Expected: FAIL because `PresentationFrameCodec` is absent.

- [ ] **Step 3: Implement bounded framing and JSON**

Use a 4-byte network-order length prefix and `JsonSerializerOptions` with camelCase property names and `JsonStringEnumConverter`. Serialize to a temporary byte array, reject frames above `65_536`, write prefix then payload, and flush. On read, fill exactly four bytes, reject invalid length before allocation, fill exactly the declared payload, deserialize one `PresentationSnapshot`, then call `PresentationSnapshotValidator.Validate`.

```csharp
public static async ValueTask WriteAsync(Stream stream, PresentationSnapshot snapshot, CancellationToken ct = default);
public static async ValueTask<PresentationSnapshot> ReadAsync(Stream stream, CancellationToken ct = default);
```

EOF before any prefix byte is `EndOfStreamException`; EOF after a partial prefix or payload is `InvalidDataException("Truncated presentation frame.")`.

- [ ] **Step 4: Write failing real pipe tests**

Use a unique pipe name per test (`$"wolpertinger.presentation.test.{Guid.NewGuid():N}"`). Start a `PresentationStatePublisher`, server, and client; assert the client receives `Current` immediately on connect and then the next full snapshot after publish. Disconnect and reconnect; assert the reconnect begins with the latest full snapshot even if no new update occurs.

Also assert the server allows one logical presentation client, detects a client disconnect even when no new snapshot is published, and rejects any client-authored byte as a protocol violation. The transport is physically duplex only for EOF detection; the logical protocol remains strictly server-to-client and exposes no gameplay command.

- [ ] **Step 5: Implement the server and client**

`PresentationPipeServer` creates one `NamedPipeServerStream` at a time with:

```csharp
new NamedPipeServerStream(
    pipeName,
    PipeDirection.InOut,
    1,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
```

After accept, create a linked per-client cancellation source and start `MonitorClientDisconnectAsync`. That monitor performs exactly one pending one-byte `ReadAsync`: EOF cancels the client session immediately; any received byte is a protocol violation and also cancels the session. Concurrently, the writer sends `_publisher.Current` and then full snapshots from `ReadUpdatesAsync(clientToken)`. Because .NET pipes permit one reader and one writer concurrently, UI death cancels the blocked update enumeration even when no new fact arrives, allowing immediate re-accept/reconnect. Track `LastSentRevision` and `ConnectedClientCount` as diagnostics only.

`PresentationPipeClient.ReadSnapshotsAsync` uses `NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous)`, never writes a byte, and yields validated snapshots until disconnect/cancellation. It does not own reconnect timing; Task 8 does.

- [ ] **Step 6: Run all transport tests and verify GREEN**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter "FullyQualifiedName~PresentationFrameCodec|FullyQualifiedName~PresentationPipeClient"
dotnet test tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj -c Release --filter FullyQualifiedName~PresentationPipeServer
```
Expected: PASS with bounded-frame and reconnect behavior proven.

- [ ] **Step 7: Commit Task 3**

```powershell
git add WOLPERTINGER.slnx src/Wolpertinger.Presentation.Contracts src/Wolpertinger.Presentation src/Wolpertinger.Edge/Presentation tests/Wolpertinger.Presentation.Tests tests/Wolpertinger.Edge.Tests
git commit -m "feat: stream presentation snapshots locally"
```

### Task 4: Build surface-neutral state, commander ViewModels, and freshness grammar

**Files:**
- Create: `src/Wolpertinger.Presentation/State/PresentationConnectionState.cs`
- Create: `src/Wolpertinger.Presentation/State/PresentationStore.cs`
- Create: `src/Wolpertinger.Presentation/ViewModels/DensityPreset.cs`
- Create: `src/Wolpertinger.Presentation/ViewModels/DockPreset.cs`
- Create: `src/Wolpertinger.Presentation/ViewModels/JumpViewModel.cs`
- Create: `src/Wolpertinger.Presentation/ViewModels/DiagnosticsViewModel.cs`
- Create: `src/Wolpertinger.Presentation/ViewModels/PresentationViewModelFactory.cs`
- Create: `src/Wolpertinger.Presentation/Preferences/PresentationPreferences.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/State/PresentationStoreTests.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/ViewModels/PresentationViewModelFactoryTests.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/Architecture/NeutralAssemblyBoundaryTests.cs`

**Interfaces:**
- Consumes: validated Task-1 snapshots and Task-3 client stream.
- Produces: monotonic `PresentationStore`, immutable `JumpViewModel`/`DiagnosticsViewModel`, `DockPreset`, `DensityPreset`, and bounded `PresentationPreferences`.

- [ ] **Step 1: Write failing store monotonicity tests**

Require these rules: higher revision replaces current; identical same revision is idempotent; same revision with different content throws integrity fault; lower revision is ignored; disconnect retains the last valid snapshot but changes connection state; unsupported/invalid snapshot never replaces the last valid one.

```csharp
var store = new PresentationStore();
Assert.True(store.ApplySnapshot(TestSnapshots.Jump(3)));
Assert.False(store.ApplySnapshot(TestSnapshots.Jump(2)));
store.MarkDisconnected();
Assert.Equal(PresentationConnectionState.Disconnected, store.State.Connection);
Assert.Equal(3UL, store.State.Snapshot!.Revision);
```

- [ ] **Step 2: Write failing commander/diagnostics grammar tests**

For a live current jump, require concise commander strings such as `42.125 ly`, `2.5 t`, and `14.75 t`. For stale/conflicting/unknown values require explicit text labels; do not rely on color. For disconnected state, preserve the last values but require `IsLive == false` and `AvailabilityText == "Disconnected — last known data"`.

```csharp
var vm = PresentationViewModelFactory.CreateJump(store.State, DensityPreset.Standard);
Assert.Equal("W. Grantler NX-42", vm.StarSystem);
Assert.Equal("42.125 ly", vm.JumpDistanceText);
Assert.True(vm.IsLive);
Assert.Equal("Current", vm.LocationStatusText);
```

`DiagnosticsViewModel` must expose the same revision/cursor/system plus full evidence digest, state digest, provenance/freshness names, FID/realm/save epoch, and connection state. It must not load a second source.

- [ ] **Step 3: Run neutral tests and verify RED**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter "FullyQualifiedName~PresentationStore|FullyQualifiedName~PresentationViewModelFactory|FullyQualifiedName~NeutralAssemblyBoundary"
```
Expected: FAIL because store/ViewModels/preferences do not exist.

- [ ] **Step 4: Implement the minimal neutral state model**

```csharp
public enum PresentationConnectionState : byte { Connecting = 0, Live = 1, Disconnected = 2, Incompatible = 3 }
public enum DockPreset : byte { Left = 1, Right = 2, Top = 3, Bottom = 4 }
public enum DensityPreset : byte { Compact = 1, Standard = 2, Expanded = 3 }
public sealed record PresentationPreferences(bool OverlayVisible, bool HubVisible, DockPreset Dock, DensityPreset Density, string? PreferredDisplayKey);
```

`PresentationPreferences` default is `OverlayVisible=true`, `HubVisible=false`, `Dock=Right`, `Density=Standard`, `PreferredDisplayKey=null`.

`PresentationStore` owns only `{Connection, Snapshot}` in memory. It has no filesystem access. `ApplySnapshot` calls the Task-1 validator before revision comparison and raises one `StateChanged` event after an accepted replacement. `MarkDisconnected` and `MarkIncompatible` change only presentation connection state.

`PresentationViewModelFactory` appends units to canonical number strings; it performs no arithmetic on jump/fuel values. Map freshness text exactly: `Current`, `Stale`, `Unknown`, `Conflicting`. Disconnection text overrides liveness but not the retained fact values.

- [ ] **Step 5: Add the architecture boundary test**

```csharp
var assembly = typeof(PresentationStore).Assembly;
Assert.DoesNotContain(assembly.GetReferencedAssemblies(), x => x.Name == "Wolpertinger.Edge");
Assert.DoesNotContain(assembly.GetReferencedAssemblies(), x => x.Name == "Wolpertinger.Presentation.Windows");
var pinvokes = assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
    .Where(m => m.GetCustomAttribute<DllImportAttribute>() is not null);
Assert.Empty(pinvokes);
```

Also assert `Wolpertinger.Presentation.Contracts` has no reference to Edge, Windows, or Avalonia.

- [ ] **Step 6: Run all Presentation neutral tests and verify GREEN**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release
```
Expected: PASS; no native desktop is required.

- [ ] **Step 7: Commit Task 4**

```powershell
git add src/Wolpertinger.Presentation tests/Wolpertinger.Presentation.Tests
git commit -m "feat: add neutral presentation state"
```

### Task 5: Implement deterministic Windows display selection, DPI placement, and topology recovery

**Files:**
- Modify: `Directory.Packages.props` — add `Avalonia`, `Avalonia.Desktop`, and `Avalonia.Themes.Fluent` at `12.1.0`.
- Create: `src/Wolpertinger.Presentation.Windows/Wolpertinger.Presentation.Windows.csproj`
- Create: `src/Wolpertinger.Presentation.Windows/Displays/DisplayDescriptor.cs`
- Create: `src/Wolpertinger.Presentation.Windows/Displays/DisplayTopologyPolicy.cs`
- Create: `src/Wolpertinger.Presentation.Windows/Displays/DockPlacementPolicy.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/Windows/DisplayTopologyPolicyTests.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/Windows/DockPlacementPolicyTests.cs`
- Modify: `WOLPERTINGER.slnx`
- Modify: `tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj`

**Interfaces:**
- Consumes: neutral `DockPreset`, `DensityPreset`, and opaque `PreferredDisplayKey`.
- Produces: adapter-local `DisplayDescriptor`, `DisplayTopologyPolicy.ResolveInitial/ResolveAfterChange`, and `DockPlacementPolicy.Resolve` returning an Avalonia `PixelRect`.

- [ ] **Step 1: Write failing display-topology tests**

Cover initial preferred display, fallback to known game display, fallback to primary, fallback to first available, zero-display failure, current-display retention, current-display removal, and the no-teleport rule when the previously preferred monitor reappears.

```csharp
var recovered = DisplayTopologyPolicy.ResolveAfterChange(
    currentDisplayKey: "MONITOR-2",
    gameDisplayKey: "MONITOR-1",
    displays: [Primary("MONITOR-1")]);
Assert.Equal("MONITOR-1", recovered.Key);

var stillRecovered = DisplayTopologyPolicy.ResolveAfterChange(
    currentDisplayKey: "MONITOR-1",
    gameDisplayKey: "MONITOR-1",
    displays: [Primary("MONITOR-1"), Secondary("MONITOR-2")]);
Assert.Equal("MONITOR-1", stillRecovered.Key);
```

- [ ] **Step 2: Write failing DPI/dock geometry tests**

Freeze these first-slice logical sizes so tests are deterministic:

| Density | Left/Right | Top/Bottom |
| --- | --- | --- |
| Compact | 320 × 160 | 560 × 104 |
| Standard | 380 × 220 | 720 × 144 |
| Expanded | 460 × 320 | 880 × 200 |

Use a 16 logical-pixel edge inset. Convert logical pixels with `Math.Round(value * Scaling, MidpointRounding.AwayFromZero)`. Clamp the result to the display working area after scaling. Left/right align to that edge and center vertically; top/bottom align to that edge and center horizontally.

Tests must cover scaling `1.0`, `1.25`, `1.5`, and `2.0`, all four docks, and a small working area that forces clamping.

- [ ] **Step 3: Run the Windows policy tests and verify RED**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter "FullyQualifiedName~DisplayTopologyPolicy|FullyQualifiedName~DockPlacementPolicy"
```
Expected: FAIL because the Windows adapter project/policies do not exist.

- [ ] **Step 4: Implement display descriptors and pure policies**

```csharp
public sealed record DisplayDescriptor(string Key, PixelRect Bounds, PixelRect WorkingArea, double Scaling, bool IsPrimary);

public static DisplayDescriptor ResolveInitial(string? preferredDisplayKey, string? gameDisplayKey, IReadOnlyList<DisplayDescriptor> displays);
public static DisplayDescriptor ResolveAfterChange(string currentDisplayKey, string? gameDisplayKey, IReadOnlyList<DisplayDescriptor> displays);
public static PixelRect Resolve(DisplayDescriptor display, DockPreset dock, DensityPreset density);
```

`Key` is an opaque adapter-owned stable string such as Avalonia `Screen.DisplayName`; never persist `Screen.TryGetPlatformHandle()` or an HWND as display identity.

- [ ] **Step 5: Verify DPI recalculation is stateless**

Add a test that resolves the same Standard/Right request first at `Scaling=1.0`, then with a replacement `DisplayDescriptor` at `Scaling=1.5`, and proves the second bounds are recomputed from logical dimensions rather than scaling the previous physical rectangle.

- [ ] **Step 6: Run all Windows policy tests and verify GREEN**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter "FullyQualifiedName~DisplayTopologyPolicy|FullyQualifiedName~DockPlacementPolicy"
```
Expected: PASS.

- [ ] **Step 7: Commit Task 5**

```powershell
git add Directory.Packages.props WOLPERTINGER.slnx src/Wolpertinger.Presentation.Windows tests/Wolpertinger.Presentation.Tests
git commit -m "feat: add Windows presentation placement policy"
```

The exact dimensions are first-slice defaults, not a public compatibility contract. Changing them later is a presentation preference change; changing the neutral boundary is not.

### Task 6: Isolate real Windows no-activate, click-through, topmost, and screen-event behavior

**Files:**
- Create: `src/Wolpertinger.Presentation.Windows/Interop/WindowsNativeMethods.cs`
- Create: `src/Wolpertinger.Presentation.Windows/Interop/WindowsExtendedStylePolicy.cs`
- Create: `src/Wolpertinger.Presentation.Windows/WindowsOverlayAdapter.cs`
- Create: `src/Wolpertinger.Presentation.Windows/WindowsHubAdapter.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/Windows/WindowsExtendedStylePolicyTests.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/Architecture/WindowsIsolationTests.cs`

**Interfaces:**
- Consumes: Avalonia `Window`, `Screens`, Task-5 topology/placement policies, neutral dock/density preferences.
- Produces: `WindowsOverlayAdapter.Attach(Window, DockPreset, DensityPreset, string? preferredDisplayKey, string? gameDisplayKey)` and `WindowsHubAdapter.Attach(Window, string? preferredDisplayKey, string? gameDisplayKey)`.

- [ ] **Step 1: Write failing native-style policy tests**

The passive overlay must add exactly these extended-style bits while preserving unrelated existing bits:

```csharp
const nint WS_EX_TRANSPARENT = 0x00000020;
const nint WS_EX_TOOLWINDOW  = 0x00000080;
const nint WS_EX_NOACTIVATE = 0x08000000;

var result = WindowsExtendedStylePolicy.ForPassiveOverlay(existing: 0x00040000);
Assert.Equal((nint)0x080400A0, result);
```

Also assert the policy does not add `WS_EX_APPWINDOW` and that the neutral Contracts/Presentation assemblies still contain no `DllImportAttribute` methods.

- [ ] **Step 2: Run native policy tests and verify RED**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter "FullyQualifiedName~WindowsExtendedStylePolicy|FullyQualifiedName~WindowsIsolation"
```
Expected: FAIL because the native policy/adapter is absent.

- [ ] **Step 3: Implement the isolated Win32 interop**

Keep every P/Invoke in `WindowsNativeMethods.cs`. Use `GetWindowLongPtr`/`SetWindowLongPtr` for `GWL_EXSTYLE = -20` and `SetWindowPos` with `HWND_TOPMOST = new(-1)`. The passive apply call uses `SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED` after styles are set.

```csharp
[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
internal static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
internal static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
```

Validate `window.TryGetPlatformHandle()?.HandleDescriptor == "HWND"` before any Win32 call. A missing/wrong handle fails the adapter operation with a presentation diagnostic; it never reaches Edge mutation.

- [ ] **Step 4: Implement overlay placement and topology subscriptions**

On `Window.Opened`, set Avalonia-level `ShowInTaskbar=false`, `SystemDecorations=None`, `CanResize=false`, `Topmost=true`, then apply the passive extended styles and Task-5 bounds. Subscribe to `window.Screens.Changed`; rebuild `DisplayDescriptor` values from `Screens.All`, resolve recovery, and recalculate bounds using the current `Screen.Scaling`/`WorkingArea`.

Do not call `window.Activate()` for the passive overlay. Setting new fact DataContext must never call focus APIs.

- [ ] **Step 5: Implement Fullscreen Hub display targeting without passive overlay styles**

`WindowsHubAdapter` uses the same initial/recovery display policy but does **not** set `WS_EX_NOACTIVATE` or `WS_EX_TRANSPARENT`. The Hub is deliberately interactive. Set its bounds to the selected screen `Bounds` and recover to game/primary if that screen disappears.

- [ ] **Step 6: Run Windows adapter tests and verify GREEN**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter "FullyQualifiedName~WindowsExtendedStylePolicy|FullyQualifiedName~WindowsIsolation|FullyQualifiedName~DisplayTopologyPolicy|FullyQualifiedName~DockPlacementPolicy"
```
Expected: PASS without opening real windows.

- [ ] **Step 7: Build the Windows adapter explicitly**

```powershell
dotnet build src/Wolpertinger.Presentation.Windows/Wolpertinger.Presentation.Windows.csproj -c Release
```
Expected: `0 Warning(s)`, `0 Error(s)` under repository-wide warnings-as-errors.

- [ ] **Step 8: Commit Task 6**

```powershell
git add src/Wolpertinger.Presentation.Windows tests/Wolpertinger.Presentation.Tests
git commit -m "feat: isolate Windows overlay behavior"
```

### Task 7: Build the Avalonia tray shell and three minimal surfaces

**Files:**
- Create: `src/Wolpertinger.Presentation.App/Wolpertinger.Presentation.App.csproj`
- Create: `src/Wolpertinger.Presentation.App/Program.cs`
- Create: `src/Wolpertinger.Presentation.App/App.axaml`
- Create: `src/Wolpertinger.Presentation.App/App.axaml.cs`
- Create: `src/Wolpertinger.Presentation.App/PresentationApplicationController.cs`
- Create: `src/Wolpertinger.Presentation.App/Views/OverlayWindow.axaml` and `.axaml.cs`
- Create: `src/Wolpertinger.Presentation.App/Views/FullscreenHubWindow.axaml` and `.axaml.cs`
- Create: `src/Wolpertinger.Presentation.App/Views/DiagnosticsWindow.axaml` and `.axaml.cs`
- Create: `src/Wolpertinger.Presentation.App/Preferences/PresentationPreferencesStore.cs`
- Create: `src/Wolpertinger.Presentation.App/Assets/wolpertinger.ico`
- Create: `tests/Wolpertinger.Presentation.Tests/Preferences/PresentationPreferencesStoreTests.cs`
- Modify: `WOLPERTINGER.slnx`

**Interfaces:**
- Consumes: Task-4 store/ViewModels/preferences and Task-6 Windows adapters.
- Produces: tray lifecycle/control plane, passive Overlay view, interactive Fullscreen Hub, read-only Diagnostics view, and safe preference persistence.

- [ ] **Step 1: Write failing preference-store tests**

Require default preferences when the file is absent, exact round-trip, corrupt JSON fallback, invalid enum fallback, and no exception escaping to block app startup.

```csharp
var store = new PresentationPreferencesStore(tempPath);
Assert.Equal(PresentationPreferencesStore.Default, await store.LoadAsync());
await store.SaveAsync(new(true, false, DockPreset.Left, DensityPreset.Compact, "DISPLAY-2"));
Assert.Equal(DockPreset.Left, (await store.LoadAsync()).Dock);
```

- [ ] **Step 2: Run preference tests and verify RED**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter FullyQualifiedName~PresentationPreferencesStore
```
Expected: FAIL because the app/preference store is absent.

- [ ] **Step 3: Create the Avalonia executable and desktop lifetime**

The project targets `net10.0`, sets `OutputType` to `WinExe`, references `Avalonia`, `Avalonia.Desktop`, and `Avalonia.Themes.Fluent`, and references Presentation + Windows projects.

```csharp
[STAThread]
public static void Main(string[] args)
    => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
```

Use the platform/default font for this slice; do not add a font package merely for template aesthetics.

In `App.OnFrameworkInitializationCompleted`, require `IClassicDesktopStyleApplicationLifetime`, set `ShutdownMode = ShutdownMode.OnExplicitShutdown`, construct one `PresentationApplicationController`, and do not assign a conventional always-open `MainWindow`.

- [ ] **Step 4: Add the tray control plane**

`App.axaml` owns one `TrayIcon` with tooltip `WOLPERTINGER` and a `NativeMenu`: `Show overlay`, `Fullscreen hub`, `Diagnostics`, separator, `Exit presentation`. Event handlers delegate directly to the controller; they contain no gameplay logic.

Create the retained minimal 32×32 `W` tray icon reproducibly from this exact ICO payload; it is a slice asset, not a final logo decision:

```powershell
$ico = 'AAABAAEAICAAAAEAIACpAAAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAAgAAAAIAgGAAAAc3p69AAAAHBJREFUeNrtlssOQBEMRP3/T/duLaQP7bglc5aq44gEYxDSFZk4niMLsouHcn4XWDVXCJxrRghEA9LHeI2ANp4+RivAW9u+S7QQa4cQgTlot1YiIApvCViLeSl7FVsKeOdQACIRnfOWAPIn1U+AECQfU087/Y03UyEAAAAASUVORK5CYII='
[IO.Directory]::CreateDirectory('src/Wolpertinger.Presentation.App/Assets') | Out-Null
[IO.File]::WriteAllBytes('src/Wolpertinger.Presentation.App/Assets/wolpertinger.ico',[Convert]::FromBase64String($ico))
```

- [ ] **Step 5: Implement the three minimal views over one current model**

Overlay XAML shows system, jump distance, remaining fuel, and availability/status text. It has no buttons in passive mode. Fullscreen Hub shows the same fields plus position, fuel used, provenance/freshness detail, and reason code. Diagnostics shows revision, cursor, profile, evidence reference/digest, state digest, provenance/freshness, and connection state.

All three receive ViewModels from the same `PresentationStore` state via `PresentationApplicationController`. Do not instantiate a second store per window.

```csharp
private void RefreshSurfaces()
{
    var state = _store.State;
    _overlay.DataContext = PresentationViewModelFactory.CreateJump(state, _preferences.Density);
    _hub.DataContext = PresentationViewModelFactory.CreateJump(state, DensityPreset.Expanded);
    _diagnostics.DataContext = PresentationViewModelFactory.CreateDiagnostics(state);
}
```

Overlay constructor attaches `WindowsOverlayAdapter`; Hub constructor attaches `WindowsHubAdapter`. Diagnostics is a normal resizable window and does not receive passive overlay Win32 styles.

- [ ] **Step 6: Implement safe preference persistence**

Default path:

```csharp
Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "WOLPERTINGER",
    "presentation.json");
```

`LoadAsync` catches `IOException`, `UnauthorizedAccessException`, and `JsonException`, validates enum values with `Enum.IsDefined`, and returns defaults on any invalid file. `SaveAsync` creates the directory, writes to `presentation.json.tmp`, flushes/disposes, then atomically replaces/moves into `presentation.json` on the same volume. Preferences contain only overlay/hub visibility, dock, density, and opaque preferred display key.

- [ ] **Step 7: Run preference tests and build the app**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter FullyQualifiedName~PresentationPreferencesStore
dotnet build src/Wolpertinger.Presentation.App/Wolpertinger.Presentation.App.csproj -c Release
```
Expected: tests PASS and app build reports `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 8: Commit Task 7**

```powershell
git add WOLPERTINGER.slnx src/Wolpertinger.Presentation.App tests/Wolpertinger.Presentation.Tests
git commit -m "feat: add tray overlay and hub shell"
```

### Task 8: Wire reconnect lifecycle and prove presentation-process failure containment with the real kernel

**Files:**
- Create: `src/Wolpertinger.Presentation/Transport/IPresentationSnapshotClient.cs`
- Create: `src/Wolpertinger.Presentation/Transport/PresentationConnectionLoop.cs`
- Modify: `src/Wolpertinger.Presentation/Transport/PresentationPipeClient.cs`
- Modify: `src/Wolpertinger.Presentation.App/PresentationApplicationController.cs`
- Modify: `src/Wolpertinger.Presentation.App/App.axaml.cs`
- Create: `tests/Wolpertinger.Presentation.Tests/Transport/PresentationConnectionLoopTests.cs`
- Create: `tests/Wolpertinger.Integration.Tests/PresentationProcessIsolationTests.cs`
- Modify: `tests/Wolpertinger.Integration.Tests/Wolpertinger.Integration.Tests.csproj`

**Interfaces:**
- Consumes: Task-3 pipe client, Task-4 `PresentationStore`, Task-7 surface controller, existing real Stage-1 `VerticalSliceRunner` and kernel fixture.
- Produces: bounded reconnect behavior and the process-level acceptance proof that UI death does not stop trusted processing.

- [ ] **Step 1: Write failing connection-loop tests**

Introduce a neutral client interface so reconnect behavior is testable without Avalonia:

```csharp
public interface IPresentationSnapshotClient
{
    IAsyncEnumerable<PresentationSnapshot> ReadSnapshotsAsync(string pipeName, CancellationToken cancellationToken = default);
}
```

Tests use a scripted fake client to prove: Connecting → Live after first valid snapshot; EOF/`IOException` → Disconnected while retaining the last snapshot; reconnect resumes with a newer snapshot; invalid contract data → Incompatible and no silent retry loop; cancellation exits cleanly.

- [ ] **Step 2: Run lifecycle tests and verify RED**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter FullyQualifiedName~PresentationConnectionLoop
```
Expected: FAIL because the connection loop/interface is absent.

- [ ] **Step 3: Implement bounded reconnect semantics**

`PresentationConnectionLoop` owns no windows. It accepts a client, store, pipe name, initial delay `250 ms`, maximum delay `2 s`, and cancellation token. After any successful snapshot, reset delay to `250 ms`. On `IOException`, `EndOfStreamException`, or `TimeoutException`, mark Disconnected and retry with exponential `min(delay * 2, 2 s)`. On `InvalidDataException` caused by incompatible/invalid contract, mark Incompatible and stop retrying until process restart.

```csharp
public async Task RunAsync(CancellationToken ct)
{
    var delay = _initialDelay;
    while (!ct.IsCancellationRequested)
    {
        _store.MarkConnecting();
        try
        {
            await foreach (var snapshot in _client.ReadSnapshotsAsync(_pipeName, ct))
            {
                _store.ApplySnapshot(snapshot);
                delay = _initialDelay;
            }
            _store.MarkDisconnected();
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or TimeoutException)
        {
            _store.MarkDisconnected();
        }
        catch (InvalidDataException)
        {
            _store.MarkIncompatible();
            return;
        }
        await Task.Delay(delay, ct);
        delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, _maximumDelay.TotalMilliseconds));
    }
}
```

- [ ] **Step 4: Wire the Avalonia controller without cross-thread UI mutation**

Parse optional `--pipe <name>` from the application command line; otherwise use `PresentationProtocol.DefaultPipeName`. Start one `PresentationConnectionLoop` for the app lifetime. Subscribe once to `PresentationStore.StateChanged` and marshal `RefreshSurfaces()` through `Dispatcher.UIThread.Post`.

`Exit presentation` cancels the connection loop, saves preferences, disposes adapters/windows, removes the tray icon through Avalonia lifetime shutdown, and calls `desktop.Shutdown()`. It does **not** signal Edge or either SPARK kernel to stop.

- [ ] **Step 5: Write the failing real-process integration test**

The test runs only on Windows. Build/use the real Ada kernel exactly like existing integration tests, create a unique pipe, instantiate `PresentationStatePublisher` + `PresentationPipeServer`, open `VerticalSliceRunner` with that publisher, launch `Wolpertinger.Presentation.App.exe --pipe <unique>`, then feed the existing `fixtures/journal/live-v4-fsdjump-session.jsonl` through the runner.

Wait until `server.LastSentRevision == 1`, record the authoritative digest, kill only the presentation process, and prove `runner.KernelDiagnostics.Lifecycle` remains synchronized. Without publishing another fact, wait until the server observes disconnect, restart the presentation app immediately on the same pipe, and require `LastSentRevision == 1` again. This proves UI restart/re-hydration does not depend on a future gameplay event.

Kill that second presentation process, wait for server disconnect, then process a second valid `FSDJump` journal line in the same bound session while no UI is connected:

```json
{"timestamp":"2026-09-09T10:00:00Z","event":"FSDJump","StarSystem":"Presentation Recovery Test","SystemAddress":10477373803,"StarPos":[1.0,2.0,3.0],"JumpDist":1.5,"FuelUsed":0.2,"FuelLevel":14.55,"BoostUsed":0}
```

Assert the trusted state digest changes and publisher revision becomes `2`. Start the presentation app a third time; wait for a successful connection and `LastSentRevision == 2`, proving reconnect receives the newest full snapshot even when the authoritative update happened while presentation was absent.

The second jump intentionally contains every field the Stage-1 `FsdJumpNormalizer` consumes: `StarSystem`, `SystemAddress`, three-element `StarPos`, `JumpDist`, `FuelUsed`, and `FuelLevel`. No test-only bypass or direct state mutation is allowed.

- [ ] **Step 6: Add a Windows runtime-style smoke assertion inside the process test**

After the first snapshot opens the overlay, enumerate top-level windows belonging to the presentation PID, find the window titled `WOLPERTINGER Overlay`, read `GWL_EXSTYLE`, and assert `WS_EX_NOACTIVATE`, `WS_EX_TRANSPARENT`, and `WS_EX_TOOLWINDOW` are all set. Keep the EnumWindows/GetWindowThreadProcessId/GetWindowLongPtr declarations inside the Windows-only integration-test helper, never in neutral production assemblies.

This smoke assertion verifies that Task-6 policy actually reached the native HWND; focus/topmost product behavior is still enforced by the adapter's `Topmost=true` plus `SetWindowPos(...HWND_TOPMOST...SWP_NOACTIVATE...)` path.

- [ ] **Step 7: Run lifecycle unit tests and real process integration test**

```powershell
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --filter FullyQualifiedName~PresentationConnectionLoop
dotnet build src/Wolpertinger.Presentation.App/Wolpertinger.Presentation.App.csproj -c Release
dotnet test tests/Wolpertinger.Integration.Tests/Wolpertinger.Integration.Tests.csproj -c Release --no-build --filter FullyQualifiedName~PresentationProcessIsolation
```
Expected: unit tests PASS; app builds 0/0; process test proves real Stage-1 fact delivery, native passive styles, UI-process kill containment, second trusted transition, and reconnect at revision 2.

- [ ] **Step 8: Commit Task 8**

```powershell
git add src/Wolpertinger.Presentation src/Wolpertinger.Presentation.App tests/Wolpertinger.Presentation.Tests tests/Wolpertinger.Integration.Tests
git commit -m "test: prove presentation process recovery"
```

### Task 9: Run the complete acceptance gate and publish only verified presentation status

**Files:**
- Modify: `README.md`
- Modify: `ROADMAP.md`
- No other product files may be changed in this closure task unless the acceptance gate exposes a concrete regression; any such regression returns to the responsible earlier task first.

**Interfaces:**
- Consumes: all Tasks 1–8 plus the unchanged Stage-1 kernel acceptance baseline.
- Produces: one verified repository state where the first presentation slice is documented truthfully and Stage 1 remains green.

- [ ] **Step 1: Run a complete clean Release build with kernel prerequisites before tests**

```powershell
dotnet clean WOLPERTINGER.slnx -c Release
dotnet restore WOLPERTINGER.slnx
dotnet build WOLPERTINGER.slnx -c Release
Push-Location kernel; alr build --validation; Pop-Location
Push-Location kernel/tests; alr build --validation; alr run; Pop-Location
Push-Location kernel/proof; alr exec -- gnatprove -P wolpertinger_kernel_proof.gpr --level=2 --report=all; Pop-Location
dotnet test WOLPERTINGER.slnx -c Release --no-build
```

Expected: .NET Release build `0 Warning(s) / 0 Error(s)`; Ada runtime tests PASS; GNATprove retains the Stage-1 proof result with no unproved checks; Edge, Presentation, and Integration suites all PASS, including `PresentationProcessIsolationTests`.

- [ ] **Step 2: Re-run actual Stage-1 host ingest/replay and assert the frozen digest**

```powershell
$kernel = (Resolve-Path 'kernel\bin\wolpertinger_kernel_main.exe').Path
$data = Join-Path $env:TEMP 'wolpertinger-presentation-acceptance'
Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
dotnet run --project src/Wolpertinger.Host -c Release --no-build -- ingest --journal fixtures/journal/live-v4-fsdjump-session.jsonl --data $data --kernel $kernel
dotnet run --project src/Wolpertinger.Host -c Release --no-build -- replay --data $data --kernel $kernel
```

Expected: ingest and replay emit the same jump output and both report exactly:

```text
691E7DEFE38100B0C0C516AA77D60E53E6115C2AC7991E9FE99CDA2BB4D66663
```

Any different digest is a Stage-1 regression blocker; do not update the expected value to make the test pass.

- [ ] **Step 3: Update public documentation only after the gate passes**

In `README.md`, keep `Foundation / pre-alpha` and `no supported end-user release`. Add a concise statement that the first presentation slice now proves real Stage-1 data through a separate tray/overlay/Fullscreen-Hub presentation process, and link both:

- `docs/superpowers/specs/2026-09-09-wolpertinger-presentation-subsystem-design.md`
- `docs/superpowers/plans/2026-09-09-wolpertinger-presentation-vertical-slice.md`

Extend the 30-second architecture diagram only enough to show:

```text
.NET context / Presentation Contract
        |
        v
separate Presentation process
  -> Tray
  -> passive Windows Overlay
  -> Fullscreen Hub
  -> Diagnostics
```

In `ROADMAP.md`, add a `Presentation subsystem — first slice` section marked complete only if Task-9 Step 1 and Step 2 passed. State the implemented scope and keep voice/CAPI/EDDN/plugins/Linux as future directions, not release promises.

- [ ] **Step 4: Run repository hygiene and dependency-boundary checks**

```powershell
git diff --check
git status --short
git grep -nEI '(api[_-]?key|client[_-]?secret|password|bearer[[:space:]]+[A-Za-z0-9_-]{16,})' -- ':!docs/superpowers/**'
dotnet test tests/Wolpertinger.Presentation.Tests/Wolpertinger.Presentation.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~NeutralAssemblyBoundary|FullyQualifiedName~WindowsIsolation"
```

Expected: no whitespace errors, no accidental credentials, and architecture-boundary tests PASS.

- [ ] **Step 5: Commit verified documentation**

```powershell
git add README.md ROADMAP.md
git commit -m "docs: document presentation vertical slice"
```

- [ ] **Step 6: Run the full gate once more after the documentation commit**

Repeat Task-9 Steps 1, 2, and 4 from the committed tree. Do not declare the slice complete from a pre-commit test run.

- [ ] **Step 7: Verify exact branch state**

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
git log --oneline --decorate -12
```

Expected: branch remains `presentation/subsystem-foundation`, working tree is clean, and history shows the task commits in order without squash/rebase/history rewrite.

## Append-only Notion Checkpoint Cadence During Execution

Update the existing page `2026-09-06 — WOLPERTINGER Public Foundation Checkpoint` (Page-ID `3d325d44-82d7-81da-a48e-e0b86d058baf`) by appending, never rewriting prior history:

1. **After Task 3:** contract + Edge projection + local snapshot transport checkpoint. Record exact branch/HEAD and test counts.
2. **After Task 6:** neutral ViewModels + Windows placement/native adapter checkpoint. Record architecture-boundary tests and Windows adapter build result.
3. **After Task 8:** real presentation-process vertical-slice checkpoint. Record the process-kill/reconnect test and whether native passive styles were observed.
4. **After Task 9:** final presentation-slice closure checkpoint with exact HEAD, complete test counts, GNATprove result, frozen Stage-1 digest, and merge/PR readiness.

If a context limit or tool/session boundary occurs before one of those milestones, append an immediate continuation handoff with current Task/Step, exact HEAD, status, tests already run, and the next unexecuted checkbox.

## Execution Stop Conditions

Stop instead of improvising if any of these occur:

- a presentation requirement appears to require changing the SPARK trusted contract merely for UI convenience;
- the second Stage-1 digest differs from `691E7DEFE38100B0C0C516AA77D60E53E6115C2AC7991E9FE99CDA2BB4D66663`;
- a neutral presentation assembly requires a Win32/HWND reference to proceed;
- Avalonia 12.1 cannot provide a normal Windows window/tray surface needed by this design and the isolated adapter cannot close the concrete gap;
- current-user pipe semantics cannot be made bounded/reconnectable without introducing a bidirectional gameplay command API;
- the process-isolation test shows UI termination affects Edge/kernel liveness or authoritative state.

A stop condition triggers only the smallest blocker analysis needed to resolve that specific issue. It does not reopen general WOLPERTINGER FuE.

## Spec Coverage Matrix

- **Presentation Contract:** Tasks 1–3 define immutable snapshots, validation, framing, Edge projection, and local transport.
- **Ownership Boundaries:** Tasks 2 and 4 keep trusted decisions in Edge and gameplay truth out of Presentation state; architecture tests enforce the dependency direction.
- **surface-neutral ViewModels / State/Data Flow:** Task 4 owns the neutral store, commander ViewModel, diagnostics projection, and no-Edge/no-Windows assembly tests.
- **Tray / Overlay / Fullscreen Hub / Diagnostics responsibilities:** Task 7 implements the four approved Release-1 surface roles over one store.
- **Windows Platform Adapter boundary:** Tasks 5–6 isolate display topology, placement, DPI, HWND styles, click-through, no-activate, and topmost behavior.
- **Multi-Monitor / DPI behavior and display-topology recovery:** Task 5 defines current-topology resolution, clamping, DPI recalculation, and no-teleport recovery; Task 6 wires `Screens.Changed`.
- **Presentation lifecycle and Failure containment:** Tasks 3 and 8 cover immediate disconnect detection, reconnect, stale retained context, process kill, and trusted-core survival.
- **Provenance / freshness UI grammar:** Tasks 1, 2, and 4 preserve source/freshness values and require explicit non-color status text.
- **Commander View != Engineering View:** Tasks 4 and 7 produce separate concise and diagnostic projections from the same snapshot.
- **First JumpFact Presentation Slice:** Tasks 2, 7, and 8 consume the real Stage-1 fact and prove tray/overlay/hub reuse plus process recovery.
- **Explicit non-goals:** Global Constraints and Execution Stop Conditions prevent voice, CAPI, EDDN, plugins, Linux implementation, full Concept UI, blank-canvas widgets, and trusted-contract expansion for UI convenience.
- **Testing strategy:** Every Task 1–8 has an explicit RED→GREEN boundary test cycle; Task 9 repeats full .NET, Ada runtime, GNATprove, real ingest/replay, hygiene, and architecture gates.
- **Acceptance criteria:** Task 9 is the final repository gate; it cannot mark the slice complete unless Stage-1 digest `691E7DEFE38100B0C0C516AA77D60E53E6115C2AC7991E9FE99CDA2BB4D66663` remains unchanged.
