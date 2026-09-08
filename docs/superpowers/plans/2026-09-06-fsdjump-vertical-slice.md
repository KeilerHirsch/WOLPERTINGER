# FSDJump Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first retained WOLPERTINGER vertical slice from a real Elite Dangerous `FSDJump` journal event through durable evidence, deterministic normalization, dual SPARK kernels, authoritative state, deterministic copilot output, and exact replay.

**Architecture:** A headless C#/.NET 10 Edge Host persists raw evidence and normalized observations, then sends bounded deterministic CBOR frames over redirected stdio to two isolated Ada/SPARK kernel processes. An in-process .NET supervisor owns active/shadow roles and authority epochs; the kernels own canonical authoritative state and return identical state digests for the same ordered input. SQLite is a disposable projection store, never the historical source of truth.

**Tech Stack:** .NET SDK 10.0.111 / C# 14; `System.Formats.Cbor` 10.0.11; `Microsoft.Data.Sqlite` 10.0.11; Ada 2022 with GNAT native 16.1.0, GPRbuild 26.0.1, GNATprove 16.1.0; `cbor_ada` 0.3.0 pinned to commit `b448c366117ff9f6c050b13d4fe609bb79495759`; SHA-256 via `System.Security.Cryptography` and `GNAT.SHA256`; xUnit for .NET tests and AUnit for Ada runtime tests.

**Spec:** `docs/superpowers/specs/2026-09-06-wolpertinger-foundation-design.md`

## Global Constraints

- WOLPERTINGER-owned source remains **EUPL-1.2 only**. Third-party dependencies retain their own licences and notices; no dependency is silently relicensed as EUPL.
- C#/.NET 10 owns volatile I/O, JSON, persistence adapters, normalization, read models, and control-plane orchestration.
- Ada/SPARK owns authoritative transitions, ordering/idempotency, canonical state, deterministic derived facts, and trusted invariants.
- Raw JSON never enters the SPARK process.
- Authoritative mutation is fail-closed on identity ambiguity, protocol error, sequence gap, cursor/digest conflict, or exact-numeric conversion failure.
- No `float`/`double` crosses the trusted boundary.
- No UI, TTS, STT, LLM, CAPI, EDDN, plugin execution, or network dependency is part of this slice.
- Active and shadow are isolated kernel processes; only one authority epoch may publish authoritative output.
- Raw evidence and normalized observations are durable append-only history; SQLite is rebuildable.
- Tests and replay must be fully offline and deterministic.

## Plan-specific implementation choices

These choices instantiate the approved spec without reopening general FuE:

- **IPC:** redirected stdin/stdout pipes, one channel per kernel process. Each frame is `uint32_be length || deterministic-CBOR-payload`. Kernel stderr is diagnostics only.
- **Supervisor placement:** a focused service inside the .NET Edge Host for v0; no third daemon. The OS may later supervise the Edge Host as a whole.
- **Evidence durability:** flush every appended record with `FileStream.Flush(flushToDisk: true)` in the first slice. Group commit is deferred until measured performance requires it.
- **Raw evidence reference:** `{segment_number, byte_offset, raw_ordinal}`; authoritative `EvidenceSequence` is allocated only by the durable normalized ledger after the raw record has committed.
- **Raw log integrity:** every record carries SHA-256 over `previous_record_digest || fixed_header || payload`, creating an integrity chain inside and across segments.
- **Segment rotation:** rotate raw and normalized logs at 16 MiB; closed segments are immutable.
- **FSDJump numerics:** `StarPos`, `JumpDist`, `FuelUsed`, and `FuelLevel` use canonical `Decimal64 { Int64 coefficient, Int8 exponent10 }` in v1. No scale-specific arithmetic is needed for this slice.
- **Realm resolver:** case-insensitive beta/test/PTS marker => `BETA_OR_PTS`; game version `4.*` => `LIVE`; `3.8*` => `LEGACY`; otherwise `UNKNOWN` and fail closed.
- **Initial SaveEpoch:** `0`. `ClearSavedGame` transition handling is not implemented in this slice, but the field is present in the contract/state key from day one.
- **Provenance/freshness v0:** every trusted observation carries an explicit provenance code. The journal adapter emits `LOCAL_JOURNAL`; successful FSDJump location/fuel facts become `CURRENT`. The SPARK kernel never consults wall-clock time to age facts; later staleness changes must arrive as explicit deterministic observations/rules.
- **Context policy:** every successfully applied `JumpFact` creates one deterministic displayable `CopilotOutput` with reason code `JumpCompleted`; speech/personality policy is deferred.
- **Digest:** each Ada kernel canonical-encodes authoritative state and hashes those bytes with `GNAT.SHA256`; the SHA wrapper is outside the SPARK proof boundary and is verified with standard/checked-in vectors. .NET independently checks returned digest length/consistency but does not define the authoritative digest.

## File map

```text
WOLPERTINGER.slnx
global.json
Directory.Build.props
Directory.Packages.props
contracts/v1/wolpertinger.cddl
fixtures/journal/live-v4-fsdjump-session.jsonl
fixtures/contracts/v1/*.hex
src/Wolpertinger.Edge/
src/Wolpertinger.Host/
tests/Wolpertinger.Edge.Tests/
tests/Wolpertinger.Integration.Tests/
kernel/alire.toml
kernel/wolpertinger_kernel.gpr
kernel/src/
kernel/tests/
kernel/proof/
```

### Task 1: Reproducible toolchain and repository skeleton

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `WOLPERTINGER.slnx`
- Create: `src/Wolpertinger.Edge/Wolpertinger.Edge.csproj`
- Create: `src/Wolpertinger.Host/Wolpertinger.Host.csproj`
- Create: `tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj`
- Create: `tests/Wolpertinger.Integration.Tests/Wolpertinger.Integration.Tests.csproj`
- Create: `kernel/alire.toml`
- Create: `kernel/wolpertinger_kernel.gpr`
- Create: `kernel/src/wolpertinger_kernel_main.adb`
- Create: `kernel/tests/alire.toml`
- Create: `kernel/proof/alire.toml`

**Interfaces:**
- Produces the build/test skeleton consumed by every later task.
- No runtime product behavior is introduced yet.

- [ ] **Step 1: Verify the implementation worktree is isolated before code starts**

Use `superpowers:using-git-worktrees` and create a worktree/branch named `stage1/fsdjump-vertical-slice` from the approved design/plan baseline. Then verify:

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
```

Expected: the stage branch is active and the worktree is clean.
- [ ] **Step 2: Install/verify the pinned build tools**

Install .NET SDK 10.0.111 and Alire 2.1.1 from their official distributions. In Alire select GNAT native 16.1.0 and GPRbuild 26.0.1, then make GNATprove 16.1.0 available to the proof crate.

```powershell
dotnet --version
alr --version
alr toolchain
gnat --version
gprbuild --version
gnatprove --version
```

Expected: .NET `10.0.111`, Alire `2.1.1`, GNAT native `16.1.0`, Alire toolchain package `gprbuild 26.0.1`, and GNATprove `16.1.0`. Note: the official Alire `gprbuild 26.0.1` package currently wraps upstream binary release `gprbuild-26.0.0-1`, so `gprbuild --version` self-reports `26.0.0`; verify the Alire package pin with `alr toolchain` as the reproducibility check.

- [ ] **Step 3: Create the .NET solution and projects**

```powershell
dotnet new sln -n WOLPERTINGER
dotnet new classlib -n Wolpertinger.Edge -o src/Wolpertinger.Edge -f net10.0
dotnet new console -n Wolpertinger.Host -o src/Wolpertinger.Host -f net10.0
dotnet new xunit -n Wolpertinger.Edge.Tests -o tests/Wolpertinger.Edge.Tests -f net10.0
dotnet new xunit -n Wolpertinger.Integration.Tests -o tests/Wolpertinger.Integration.Tests -f net10.0
dotnet sln WOLPERTINGER.slnx add src/Wolpertinger.Edge/Wolpertinger.Edge.csproj src/Wolpertinger.Host/Wolpertinger.Host.csproj tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj tests/Wolpertinger.Integration.Tests/Wolpertinger.Integration.Tests.csproj
dotnet add src/Wolpertinger.Host/Wolpertinger.Host.csproj reference src/Wolpertinger.Edge/Wolpertinger.Edge.csproj
dotnet add tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj reference src/Wolpertinger.Edge/Wolpertinger.Edge.csproj
dotnet add tests/Wolpertinger.Integration.Tests/Wolpertinger.Integration.Tests.csproj reference src/Wolpertinger.Edge/Wolpertinger.Edge.csproj
```
- [ ] **Step 4: Pin .NET package versions and strict compiler defaults**

Create `global.json` exactly as:

```json
{
  "sdk": {
    "version": "10.0.111",
    "rollForward": "disable",
    "allowPrerelease": false
  }
}
```

Set `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `Deterministic=true`, and `LangVersion=14.0` in `Directory.Build.props`. Set `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` in `Directory.Packages.props` and pin:

```xml
<PackageVersion Include="System.Formats.Cbor" Version="10.0.11" />
<PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.11" />
```

Add the two package references without inline versions to `Wolpertinger.Edge.csproj`.

- [ ] **Step 5: Create the Ada crate manifests with a pinned CBOR dependency**

`kernel/alire.toml` must include:

```toml
name = "wolpertinger_kernel"
version = "0.1.0-dev"
description = "WOLPERTINGER deterministic trusted kernel"
licenses = "EUPL-1.2"

[[depends-on]]
cbor_ada = "=0.3.0"

[[pins]]
cbor_ada = { url = "https://github.com/b-erdem/cbor_ada.git", commit = "b448c366117ff9f6c050b13d4fe609bb79495759" }
```

`cbor_ada` remains Apache-2.0 licensed; do not copy its source under the WOLPERTINGER EUPL header. Record the exact version/commit and Apache-2.0 licence in `NOTICE.md`. The European Commission EUPL FAQ treats permissive licences including Apache-2.0 as inbound-compatible dependencies; this does not make Apache-2.0 an EUPL Appendix outbound-compatible licence and does not relicense the dependency.

Keep AUnit and GNATprove in nested crates rather than runtime dependencies. `kernel/tests/alire.toml` must contain:

```toml
name = "wolpertinger_kernel_tests"
version = "0.1.0-dev"
licenses = "EUPL-1.2"

[[depends-on]]
wolpertinger_kernel = "*"
aunit = "=26.0.0"

[[pins]]
wolpertinger_kernel = { path = ".." }
```

`kernel/proof/alire.toml` must contain:

```toml
name = "wolpertinger_kernel_proof"
version = "0.1.0-dev"
licenses = "EUPL-1.2"

[[depends-on]]
wolpertinger_kernel = "*"
gnatprove = "=16.1.0"

[[pins]]
wolpertinger_kernel = { path = ".." }
```
- [ ] **Step 6: Make the empty skeleton build before adding behavior**

```powershell
dotnet restore WOLPERTINGER.slnx
dotnet build WOLPERTINGER.slnx -c Release
Push-Location kernel
alr update
alr build
Pop-Location
```

Expected: both .NET and Ada skeletons build cleanly with warnings treated as errors.

- [ ] **Step 7: Commit the reproducible skeleton**

```powershell
git add global.json Directory.Build.props Directory.Packages.props WOLPERTINGER.slnx src tests kernel
git commit -m "build: establish vertical slice toolchains"
```

### Task 2: Freeze protocol v1 and cross-language golden vectors

**Files:**
- Create: `contracts/v1/wolpertinger.cddl`
- Create: `src/Wolpertinger.Edge/Contracts/Decimal64.cs`
- Create: `src/Wolpertinger.Edge/Contracts/ObservationEnvelope.cs`
- Create: `src/Wolpertinger.Edge/Contracts/KernelProtocol.cs`
- Create: `src/Wolpertinger.Edge/Contracts/CborContractCodec.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Contracts/Decimal64Tests.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Contracts/CborContractCodecTests.cs`
- Create: `fixtures/contracts/v1/session-bound.hex`
- Create: `fixtures/contracts/v1/fsdjump.hex`
- Create: `kernel/src/wolpertinger_types.ads`
- Create: `kernel/src/wolpertinger_protocol.ads`
- Create: `kernel/src/wolpertinger_protocol.adb`

**Interfaces:**
- Produces `Decimal64`, `ObservationEnvelope`, host/kernel frame kinds, and byte-identical CBOR golden vectors.
- Consumed by normalization, ledger, kernel engine, and IPC tasks.
- [ ] **Step 1: Write the protocol-v1 CDDL before either implementation**

Use these stable integer IDs in `contracts/v1/wolpertinger.cddl`:

```text
decimal64 = [coefficient: int, exponent10: -18..18]
cursor = [evidence-sequence: uint, message-ordinal: uint]
profile-key = [fid: tstr .size (1..64), realm: 0..3, save-epoch: uint]

host-message = set-role / apply-observation
set-role = { 0: 1, 1: uint, 2: 0..1 }
apply-observation = {
  0: 2, 1: 1, 2: cursor, 3: bstr .size 32, 4: bstr .size 16,
  5: profile-key, 6: 1..2, 7: int / null, 8: int, 9: int,
  10: 1..65535, 11: any, 12: 0..5
}
fsdjump-payload = {
  0: tstr .size (1..128), 1: uint,
  2: [decimal64, decimal64, decimal64],
  3: decimal64, 4: decimal64, 5: decimal64
}
```

Observation kind `1` is `SessionBound`; kind `2` is `FsdJump`. Realm values are `0=UNKNOWN`, `1=LIVE`, `2=LEGACY`, `3=BETA_OR_PTS`. Role values are `0=SHADOW`, `1=ACTIVE`.

Fields `7`, `8`, and `9` are UTC Unix epoch milliseconds: nullable `SourceTimeUtc`, `ObservedUtc`, and `CommitUtc` respectively. `MonotonicTick` never crosses this replay contract.

Field `12` is source provenance: `0=UNKNOWN`, `1=LOCAL_JOURNAL`, `2=LOCAL_STATUS`, `3=FRONTIER_API`, `4=COMMUNITY`, `5=USER_ENTERED`. The first slice emits only `LOCAL_JOURNAL`; the remaining codes reserve stable semantics without adding their adapters.

Field `10` is `MessageCount`. It is constant for every message derived from one raw evidence record; `SessionBound` and `FsdJump` both use `1` in v0. Kernel ordering uses `{EvidenceSequence, MessageOrdinal, MessageCount}` as follows: after ordinal `n`, if `n + 1 < MessageCount`, the only valid next cursor is the same sequence with ordinal `n + 1`; after the last ordinal, the only valid next cursor is the next sequence with ordinal `0`. This keeps the public delivery cursor `{EvidenceSequence, MessageOrdinal}` while removing ambiguity when a future evidence record expands into multiple bounded messages.

- [ ] **Step 2: Write failing Decimal64 canonicalization tests in .NET**

```csharp
[Theory]
[InlineData("36.0340", 36034L, -3)]
[InlineData("3.6034E1", 36034L, -3)]
[InlineData("0.000000", 0L, 0)]
public void ParseCanonicalizesEquivalentLexemes(string text, long coefficient, sbyte exponent)
{
    var value = Decimal64.Parse(text);
    Assert.Equal(new Decimal64(coefficient, exponent), value);
}
```

Add rejection tests for coefficient overflow and exponents outside `-18..18`.
- [ ] **Step 3: Implement `Decimal64` with lexical parsing only**

Implement `Decimal64.Parse(string)` without using `double`, `float`, or `decimal`. Parse sign, digits, decimal point, and optional `E/e` exponent directly into an `Int64` coefficient and bounded base-10 exponent, then canonicalize by removing trailing coefficient zeros.

```csharp
public readonly record struct Decimal64(long Coefficient, sbyte Exponent10)
{
    public static Decimal64 Parse(string lexical);
    public override string ToString();
}
```

Run:

```powershell
dotnet test tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj -c Release --filter FullyQualifiedName~Decimal64Tests
```

Expected: PASS.

- [ ] **Step 4: Write failing deterministic-CBOR golden-vector tests**

Construct one `SessionBound` envelope and one `FsdJump` envelope with fixed IDs/timestamps. Assert that `CborContractCodec.Encode(...)` exactly matches the checked-in `.hex` vector and that decoding returns the same typed value.

```csharp
var encoded = CborContractCodec.Encode(observation);
Assert.Equal(Convert.FromHexString(File.ReadAllText(vectorPath).Trim()), encoded);
Assert.Equal(observation, CborContractCodec.DecodeObservation(encoded));
```

- [ ] **Step 5: Implement the .NET CBOR codec using canonical mode and explicit bounds**

Use `CborWriter(CborConformanceMode.Canonical)` and `CborReader(..., CborConformanceMode.Canonical)`. Reject indefinite lengths, duplicate/unknown internal keys, trailing bytes, invalid UTF-8, unsupported tags, and frames whose decoded structure exceeds the v1 schema.

The codec must encode maps in ascending integer-key order rather than relying on reflection or serializer defaults.
- [ ] **Step 6: Implement the Ada v1 contract types and decode the same golden vectors**

Define bounded Ada/SPARK types corresponding exactly to the CDDL. Keep byte parsing in `Wolpertinger_Protocol`; domain state packages must not know CBOR.

```ada
subtype Exponent_10 is Integer range -18 .. 18;
type Decimal_64 is record
   Coefficient : Interfaces.Integer_64;
   Exponent    : Exponent_10;
end record;

type Observation_Cursor is record
   Evidence_Sequence : Interfaces.Unsigned_64;
   Message_Ordinal   : Interfaces.Unsigned_32;
end record;
```

Use `cbor_ada` only through a narrow wrapper. Reject tags, floats, indefinite containers, unknown internal keys, excessive text lengths, and trailing bytes even if the underlying library can represent them.

- [ ] **Step 7: Add Ada runtime tests for the checked-in golden vectors**

The nested test crate must read `fixtures/contracts/v1/*.hex`, decode each vector, assert exact fields, re-encode, and compare byte-for-byte with the original vector.

```powershell
Push-Location kernel/tests
alr build
alr run
Pop-Location
```

Expected: all protocol vector tests pass.

- [ ] **Step 8: Commit the contract as one reviewable unit**

```powershell
git add contracts fixtures/contracts src/Wolpertinger.Edge/Contracts tests/Wolpertinger.Edge.Tests/Contracts kernel/src/wolpertinger_types.ads kernel/src/wolpertinger_protocol.* kernel/tests
git commit -m "feat: define trusted protocol v1"
```

### Task 3: Append-only raw evidence log and crash recovery

**Files:**
- Create: `src/Wolpertinger.Edge/Evidence/RawEvidence.cs`
- Create: `src/Wolpertinger.Edge/Evidence/EvidenceReference.cs`
- Create: `src/Wolpertinger.Edge/Evidence/SegmentedEvidenceLog.cs`
- Create: `src/Wolpertinger.Edge/Evidence/EvidenceLogRecovery.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Evidence/SegmentedEvidenceLogTests.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Evidence/EvidenceLogRecoveryTests.cs`

**Interfaces:**
- Produces `RawEvidenceReceipt AppendAsync(RawEvidenceInput, CancellationToken)` and deterministic recovery enumeration in append order.
- Normalization cannot begin until an append returns a durable receipt.
- [ ] **Step 1: Write failing durability/framing tests**

Cover append/read-back, segment rotation, chained digest validation, and byte-offset references. Use a temporary directory and a small test-only segment limit so rotation is deterministic.

```csharp
var receipt = await log.AppendAsync(input, ct);
Assert.True(receipt.IsDurable);
Assert.Equal(input.Payload.ToArray(), recovered.Single().Payload);
Assert.Equal(0UL, recovered.Single().Reference.RawOrdinal);
```

- [ ] **Step 2: Implement the v1 raw-record frame**

Use this big-endian fixed header before the raw UTF-8 JSON payload:

```text
magic[4] = "WLEV"
format_version:u16 = 1
source_kind:u16
raw_ordinal:u64
observed_unix_ms:i64
payload_length:u32
payload[payload_length]
previous_digest[32]
record_digest[32]
```

`record_digest = SHA256(previous_digest || fixed_header || payload)`, where `fixed_header` includes magic, format version, source kind, raw ordinal, observed time, and payload length.
- [ ] **Step 3: Make append durable before returning**

Open the active segment with asynchronous sequential I/O, append the complete frame, then call `Flush(flushToDisk: true)` before constructing `RawEvidenceReceipt`.

```csharp
await stream.WriteAsync(frame, cancellationToken);
stream.Flush(flushToDisk: true);
return new RawEvidenceReceipt(reference, payloadDigest, observedUtc, DateTimeOffset.UtcNow, IsDurable: true);
```

The receipt's `CommitUtc` is captured after the successful durable flush. It is metadata only and never participates in authoritative ordering.

- [ ] **Step 4: Write failing crash-tail and corruption tests**

Create three cases: truncated payload, truncated digest, and a flipped byte in a completed middle record. Tail truncation must recover to the previous valid boundary; mid-history corruption must raise `EvidenceCorruptionException` and must not skip forward.

- [ ] **Step 5: Implement startup recovery and 16 MiB segment rotation**

Recovery scans segments in numeric order, validates the digest chain, and truncates only an incomplete/invalid final tail record in the active final segment. A closed segment with any invalid record is corruption.

- [ ] **Step 6: Run evidence tests and commit**

```powershell
dotnet test tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj -c Release --filter FullyQualifiedName~Evidence
git add src/Wolpertinger.Edge/Evidence tests/Wolpertinger.Edge.Tests/Evidence
git commit -m "feat: add durable evidence log"
```

### Task 4: Session binding, exact FSDJump normalization, and normalized ledger

**Files:**
- Create: `fixtures/journal/live-v4-fsdjump-session.jsonl`
- Create: `src/Wolpertinger.Edge/Journal/JournalEventClassifier.cs`
- Create: `src/Wolpertinger.Edge/Journal/GalaxyRealmResolver.cs`
- Create: `src/Wolpertinger.Edge/Journal/SessionIdentityTracker.cs`
- Create: `src/Wolpertinger.Edge/Journal/FsdJumpNormalizer.cs`
- Create: `src/Wolpertinger.Edge/Persistence/NormalizedObservationLedger.cs`
- Create: `src/Wolpertinger.Edge/Persistence/NormalizationDisposition.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Journal/SessionIdentityTrackerTests.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Journal/FsdJumpNormalizerTests.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Persistence/NormalizedObservationLedgerTests.cs`

**Interfaces:**
- `SessionIdentityTracker.Observe(RawEvidenceReceipt, JsonElement)` produces either `IdentityPending`, `SessionBoundDraft`, `IdentityConflict`, or no authoritative message.
- `FsdJumpNormalizer.Normalize(...)` produces one `ObservationEnvelopeDraft` only when session identity is bound.
- `NormalizedObservationLedger.CommitAsync(...)` allocates the next `EvidenceSequence` only for dispatchable observation groups and durably records ignored/raw-only dispositions separately.

- [ ] **Step 1: Check in a minimal real-shape offline journal fixture**

The fixture contains one `Fileheader`, one `Commander`, one `LoadGame`, one unrelated forward-compatible/ignored event, and one `FSDJump`. Use synthetic commander/FID values but real Frontier field shapes; no personal journal data enters the repository.
- [ ] **Step 2: Write failing realm/session tests**

Cover `4.x -> LIVE`, `3.8x -> LEGACY`, beta/test marker precedence, unknown versions, FID mismatch, and FSDJump-before-binding. Derive `SessionId` deterministically from the first 16 bytes of `SHA256("session-v1" || FileheaderEvidenceDigest)`.

```csharp
var result = tracker.Observe(fileHeaderReceipt, fileHeaderJson);
Assert.Equal(GalaxyRealm.Live, result.Realm);
Assert.Equal(expectedSessionId, result.SessionId);
```

- [ ] **Step 3: Implement session identity tracking without owning authoritative game state**

The .NET tracker only establishes transport context. It never applies location/fuel state itself. A bound session yields `ProfileKey(Fid, Realm, SaveEpoch: 0)` and a `SessionBound` observation draft; a conflict yields a diagnostic and no mutation message. Both `SessionBound` and `FsdJump` drafts from the journal adapter set `Provenance=LocalJournal`.

- [ ] **Step 4: Write failing lexical numeric normalization tests**

For `StarPos`, `JumpDist`, `FuelUsed`, and `FuelLevel`, read the JSON number token as raw lexical text and pass it to `Decimal64.Parse`. Include scientific notation and an intentionally overflowing coefficient.

```csharp
Assert.Equal(new Decimal64(55359, -3), normalized.JumpDistance);
Assert.Equal(new Decimal64(4843642, -6), normalized.FuelUsed);
```

No test may call `GetDouble()`, `GetSingle()`, or convert through `decimal`.
- [ ] **Step 5: Implement FSDJump normalization with only the required fields**

Require `StarSystem`, `SystemAddress`, 3-element `StarPos`, `JumpDist`, `FuelUsed`, and `FuelLevel`. Unknown extra Frontier fields are ignored by the normalizer but remain present in raw evidence.

```csharp
public sealed record FsdJumpPayload(
    string StarSystem,
    ulong SystemAddress,
    Decimal64 X,
    Decimal64 Y,
    Decimal64 Z,
    Decimal64 JumpDistance,
    Decimal64 FuelUsed,
    Decimal64 FuelLevel);
```

- [ ] **Step 6: Write failing normalized-ledger sequencing tests**

Prove these cases: raw record committed but normalization crashes before ledger append; ignored external event gets a durable `Ignored` disposition but consumes no kernel sequence; `SessionBound` receives sequence 1; `FsdJump` receives sequence 2; restart recovers the next sequence as 3 without duplication.

- [ ] **Step 7: Implement the durable normalized ledger**

Use a length-framed deterministic-CBOR ledger record plus SHA-256 chain. Store `RawOrdinal`, evidence reference/digest, normalizer version `1`, numeric-semantics version `1`, disposition, and, only for dispatchable records, the `EvidenceSequence` and typed envelope bytes. Flush to disk before dispatch.

- [ ] **Step 8: Run normalization/ledger tests and commit**

```powershell
dotnet test tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj -c Release --filter "FullyQualifiedName~Journal|FullyQualifiedName~NormalizedObservationLedger"
git add fixtures/journal src/Wolpertinger.Edge/Journal src/Wolpertinger.Edge/Persistence tests/Wolpertinger.Edge.Tests/Journal tests/Wolpertinger.Edge.Tests/Persistence
git commit -m "feat: normalize sequenced FSDJump observations"
```

### Task 5: SPARK authoritative state machine and invariants

**Files:**
- Create: `kernel/src/wolpertinger_bounded_text.ads`
- Create: `kernel/src/wolpertinger_bounded_text.adb`
- Create: `kernel/src/wolpertinger_state.ads`
- Create: `kernel/src/wolpertinger_state.adb`
- Create: `kernel/src/wolpertinger_facts.ads`
- Create: `kernel/src/wolpertinger_engine.ads`
- Create: `kernel/src/wolpertinger_engine.adb`
- Create: `kernel/tests/src/test_engine.adb`
- Create: `kernel/proof/wolpertinger_kernel_proof.gpr`

**Interfaces:**
- `Wolpertinger_Engine.Apply(State, Observation) -> Apply_Result` is the only authoritative transition entry point.
- Produces deterministic `Jump_Fact` only after a successful `FsdJump` transition. `Jump_Fact` includes source provenance and freshness copied from the authoritative post-transition location/fuel state.
- The engine contains no file, network, clock, process, or UI calls.

- [ ] **Step 1: Write failing Ada tests for session binding and FSDJump application**

Test clean-state `SessionBound`, then `FsdJump`, and assert profile/session identity, destination `SystemAddress`, bounded `StarSystem`, position, fuel, and last cursor.

```ada
Result := Wolpertinger_Engine.Apply (State, Session_Bound_Observation);
AUnit.Assertions.Assert (Result.Status = Applied, "session must bind");
Result := Wolpertinger_Engine.Apply (State, FSD_Jump_Observation);
AUnit.Assertions.Assert (State.Location.System_Address = Expected_Address, "jump must update location");
AUnit.Assertions.Assert (State.Location.Provenance = Local_Journal, "jump provenance must be explicit");
AUnit.Assertions.Assert (State.Location.Freshness = Current, "fresh journal location must be current");
```
- [ ] **Step 2: Write failing ordering/idempotency/fencing-independent engine tests**

Cover: same last cursor + same digest => `Idempotent`; same cursor + different digest => `Integrity_Fault`; future cursor => `Sequence_Gap`; FSDJump before binding => `Identity_Conflict`; FID/realm/session mismatch after binding => `Identity_Conflict` without state mutation.

- [ ] **Step 3: Implement bounded strings and domain types**

Use fixed-capacity records instead of heap strings inside SPARK:

```ada
type Text_128 is record
   Length : Natural range 0 .. 128 := 0;
   Data   : String (1 .. 128) := (others => Character'Val (0));
end record;
```

Provide constructors with explicit length/UTF-8-byte bounds at the protocol boundary. State packages operate on the bounded type only.

- [ ] **Step 4: Implement the state machine with a single serialized transition lane**

`Kernel_State` contains bound profile/session, last accepted cursor/digest, location, fuel, provenance/freshness metadata, and last jump data required for `Jump_Fact`. Define `Provenance_Kind` and `Freshness_State` (`Unknown`, `Current`, `Stale`, `Conflicting`) as bounded enums. A successful local-journal FSDJump sets location and fuel provenance to `Local_Journal` and freshness to `Current`. `Apply` computes a candidate next state, checks all preconditions, then commits the candidate atomically to the in-memory record only on `Applied`.

- [ ] **Step 5: Add SPARK contracts for the core invariants**

Prove at minimum: rejected/idempotent inputs do not mutate authoritative state; successful application advances exactly to the supplied cursor; a successful FSDJump requires bound matching identity; applied location/fuel metadata preserves a valid explicit provenance/freshness enum; Decimal64 exponent remains in `-18..18`; bounded text lengths never exceed capacity.

Run:

```powershell
Push-Location kernel/proof
alr exec -- gnatprove -P wolpertinger_kernel_proof.gpr --level=2 --report=all
Pop-Location
```

Expected: 0 unproved obligations in the declared engine/state proof boundary.
- [ ] **Step 6: Run Ada runtime tests and proof, then commit**

```powershell
Push-Location kernel/tests
alr build
alr run
Pop-Location
Push-Location kernel/proof
alr exec -- gnatprove -P wolpertinger_kernel_proof.gpr --level=2 --report=all
Pop-Location
git add kernel/src/wolpertinger_bounded_text.* kernel/src/wolpertinger_state.* kernel/src/wolpertinger_facts.ads kernel/src/wolpertinger_engine.* kernel/tests kernel/proof
git commit -m "feat: add deterministic SPARK state machine"
```

### Task 6: Kernel executable, stdio framing, canonical state digest

**Files:**
- Create: `kernel/src/wolpertinger_framing.ads`
- Create: `kernel/src/wolpertinger_framing.adb`
- Create: `kernel/src/wolpertinger_state_encoding.ads`
- Create: `kernel/src/wolpertinger_state_encoding.adb`
- Create: `kernel/src/wolpertinger_digest.ads`
- Create: `kernel/src/wolpertinger_digest.adb`
- Modify: `kernel/src/wolpertinger_kernel_main.adb`
- Create: `kernel/tests/src/test_state_digest.adb`

**Interfaces:**
- stdin consumes `uint32_be length || CBOR host-message` frames.
- stdout produces `uint32_be length || CBOR kernel-response` frames only.
- stderr is non-protocol diagnostics.
- Each successful/idempotent observation result includes the current 32-byte SHA-256 state digest.
- [x] **Step 1: Write failing frame parser tests**

Cover zero length, length above 64 KiB, truncated frame, trailing bytes after a complete CBOR message, and two consecutive valid frames. Set the v1 maximum kernel payload to `65_536` bytes.

- [x] **Step 2: Implement strict big-endian stdio framing**

Read exactly four bytes, decode an unsigned big-endian length, reject `0` or `> 65_536`, then read exactly that many bytes. Never scan for magic bytes or attempt resynchronization inside a corrupted IPC stream; fail the kernel process so the supervisor can restart/replay it.

- [x] **Step 3: Define canonical authoritative-state bytes**

Encode state as a fixed-order CBOR array, not a map or memory dump:

```text
[
  state-schema-version=1,
  is-bound, session-id, fid, realm, save-epoch,
  last-evidence-sequence, last-message-ordinal,
  has-location, system-address, star-system, location-provenance, location-freshness,
  fuel-provenance, fuel-freshness,
  [x-decimal64, y-decimal64, z-decimal64],
  fuel-level-decimal64, fuel-used-decimal64, last-jump-distance-decimal64
]
```

For an unbound/absent field use the schema-defined neutral value; do not omit array positions.

- [x] **Step 4: Hash the canonical state with `GNAT.SHA256`**

Keep the SHA wrapper outside the SPARK proof boundary. Add a test that compares the Ada digest against a checked-in expected SHA-256 value generated from the exact canonical bytes.
- [x] **Step 5: Implement monotonic role/epoch control in the kernel executable**

Each process starts as `SHADOW` at epoch `0`. `SetRole(newEpoch, role)` is accepted only when `newEpoch > currentEpoch`; stale/equal role changes are rejected. Role/epoch are control state and are excluded from the authoritative state digest.

Both roles apply observations and return facts/digests. The host may surface facts only from the kernel whose response epoch equals the supervisor's current epoch and whose role is active.

- [x] **Step 6: Complete the kernel main loop and response encoding**

For each host frame: decode -> validate -> apply/control -> compute digest -> encode one response -> flush stdout. Protocol responses include response kind, status, current epoch, the kernel's current role, cursor when applicable, state digest, and optional `JumpFact`.

No normal logging goes to stdout.

Framing corruption (truncated header/payload, zero length, or oversize length) terminates the kernel process with a non-zero exit and no protocol stdout. A complete frame containing invalid CBOR/schema data returns a deterministic `INVALID_MESSAGE` response instead of attempting stream resynchronization.

- [x] **Step 7: Run kernel protocol/digest tests and commit**

```powershell
Push-Location kernel/tests
alr build
alr run
Pop-Location
Push-Location kernel/proof
alr exec -- gnatprove -P wolpertinger_kernel_proof.gpr --level=2 --report=all
Pop-Location
git add kernel/src kernel/tests kernel/proof fixtures/contracts
git commit -m "feat: expose framed trusted kernel process"
```

### Task 7: .NET kernel client and Active/Shadow supervisor

**Files:**
- Create: `src/Wolpertinger.Edge/Kernel/KernelProcessClient.cs`
- Create: `src/Wolpertinger.Edge/Kernel/KernelProcessOptions.cs`
- Create: `src/Wolpertinger.Edge/Kernel/AuthorityEpochStore.cs`
- Create: `src/Wolpertinger.Edge/Kernel/KernelSupervisor.cs`
- Create: `src/Wolpertinger.Edge/Kernel/KernelDivergenceException.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Kernel/AuthorityEpochStoreTests.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Kernel/KernelSupervisorTests.cs`

**Interfaces:**
- `KernelProcessClient.StartAsync`, `SetRoleAsync`, `ApplyAsync`, and `StopAsync` wrap one Ada child process.
- `KernelSupervisor.ApplyAsync(ObservationEnvelope)` fans out identical bytes to both kernels, compares cursor/digest, and returns only fenced Active output.
- [x] **Step 1: Write failing epoch-store tests**

Use an append-only `control/authority-epochs.bin` record of `epoch:u64_be || sha256("WLEP-v1" || epoch)`. Test clean creation, monotonic increments, truncated-tail recovery, and corrupted completed record rejection.

```csharp
Assert.Equal(1UL, await store.NextAsync(ct));
Assert.Equal(2UL, await store.NextAsync(ct));
```

- [x] **Step 2: Implement `IKernelProcessClient` and the real stdio client**

Introduce an internal interface for deterministic unit fakes and one production implementation that launches `wolpertinger_kernel[.exe]` with redirected stdin/stdout/stderr. Serialize writes through one async lock per process and enforce one response per request.

```csharp
Task SetRoleAsync(ulong epoch, KernelRole role, CancellationToken ct);
Task<KernelApplyResult> ApplyAsync(ObservationEnvelope observation, CancellationToken ct);
```

If framing, EOF, timeout, protocol decoding, or child exit occurs, mark that client unhealthy and stop using the channel.

- [x] **Step 3: Write failing supervisor fan-out/fencing tests with fake clients**

Assert that identical envelope bytes are sent to Active and Shadow, both results must report the same cursor/digest, only the Active fact is returned, stale-epoch output is rejected, and mismatched digests raise `KernelDivergenceException`.

- [x] **Step 4: Implement the minimal supervisor state machine**

On startup allocate a fresh epoch, start two kernels, assign A=`ACTIVE`, B=`SHADOW`, and retain the last agreed cursor/digest. `ApplyAsync` waits for both results before publishing an Active fact under normal healthy operation.
- [x] **Step 5: Add failover/rejoin tests before implementation**

Cover two paths. Shadow failure: Active remains authoritative, a new Shadow is started at the current epoch and catches up by replay. Active failure: require a valid caught-up Shadow result, allocate a new epoch, promote Shadow, reject any late old-epoch Active response, then restart/catch up the failed process as the new Shadow.

- [x] **Step 6: Implement replay-assisted kernel rejoin**

Add `IObservationReplaySource` to the persistence boundary:

```csharp
IAsyncEnumerable<ObservationEnvelope> ReadObservationsAsync(
    ObservationCursor? after,
    CancellationToken cancellationToken);
```

A fresh kernel replays from sequence 1; a future checkpoint may supply `after`. Rejoin succeeds only when the rebuilt Shadow reaches the supervisor's current agreed cursor and state digest.

- [x] **Step 7: Run supervisor tests and commit**

```powershell
dotnet test tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj -c Release --filter FullyQualifiedName~Kernel
git add src/Wolpertinger.Edge/Kernel src/Wolpertinger.Edge/Persistence tests/Wolpertinger.Edge.Tests/Kernel
git commit -m "feat: supervise active and shadow kernels"
```

### Task 8: JumpFact, deterministic context decision, and copilot output

**Files:**
- Create: `src/Wolpertinger.Edge/Facts/JumpFact.cs`
- Create: `src/Wolpertinger.Edge/Context/ContextDecision.cs`
- Create: `src/Wolpertinger.Edge/Context/ContextDecisionEngine.cs`
- Create: `src/Wolpertinger.Edge/Output/CopilotOutput.cs`
- Create: `src/Wolpertinger.Edge/Output/CopilotOutputFormatter.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Context/ContextDecisionEngineTests.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Output/CopilotOutputFormatterTests.cs`

**Interfaces:**
- Consumes the fenced Active `JumpFact` returned by `KernelSupervisor`.
- Produces a plain headless `CopilotOutput` with no AI or presentation dependency.
- [x] **Step 1: Write failing context/output tests**

For one fixed `JumpFact`, assert one `Surface=true` decision with reason code `JumpCompleted`, channel `Display`, and no speech/AI side effect. Assert identical input produces byte-for-byte identical UTF-8 output text.

```csharp
var decision = engine.Decide(fact);
Assert.True(decision.Surface);
Assert.Equal("JumpCompleted", decision.ReasonCode);
Assert.Equal(OutputChannel.Display, decision.Channel);
```

- [x] **Step 2: Implement the first deterministic policy**

The v0 policy deliberately has one rule: a newly applied authoritative `JumpFact` is display-worthy. Idempotent kernel results do not create a second output.

- [x] **Step 3: Implement invariant formatting without floating point**

Format using `Decimal64.ToString()` and stable punctuation:

```text
Jump complete: <StarSystem> - <JumpDistance> ly, fuel <FuelLevel> t.
```

`CopilotOutput` also carries the observation cursor, evidence digest/reference, state digest, and reason code so diagnostics can answer why it was emitted.

- [x] **Step 4: Run tests and commit**

```powershell
dotnet test tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj -c Release --filter "FullyQualifiedName~Context|FullyQualifiedName~Output"
git add src/Wolpertinger.Edge/Facts src/Wolpertinger.Edge/Context src/Wolpertinger.Edge/Output tests/Wolpertinger.Edge.Tests/Context tests/Wolpertinger.Edge.Tests/Output
git commit -m "feat: add deterministic jump copilot output"
```

### Task 9: Rebuildable SQLite projections

**Files:**
- Create: `src/Wolpertinger.Edge/Projections/ProjectionStore.cs`
- Create: `src/Wolpertinger.Edge/Projections/ProjectionRebuilder.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Projections/ProjectionStoreTests.cs`
- Create: `tests/Wolpertinger.Edge.Tests/Projections/ProjectionRebuilderTests.cs`

**Interfaces:**
- `ProjectionStore.ApplyAsync(CopilotOutput, CancellationToken)` writes query/read-model state only.
- `ProjectionRebuilder.RebuildAsync(...)` deletes/recreates the database from replay-produced outputs.
- No kernel or replay code reads SQLite to determine authoritative truth.

- [x] **Step 1: Write failing projection tests**

Assert schema creation, one latest-jump row per profile, one output row per cursor, idempotent re-application, and exact preservation of state/evidence digests.

- [x] **Step 2: Implement the minimal schema with raw SQL**

Use `Microsoft.Data.Sqlite` directly; no ORM. Create `projection_meta`, `latest_jump`, and `copilot_output`. Store `SystemAddress` as invariant decimal text to avoid accidental signed conversion; store digests as 32-byte BLOBs and Decimal64 display values as canonical text.

- [x] **Step 3: Write a failing delete-and-rebuild test**

Populate projections, close the database, delete `projections.db`, run the rebuilder from deterministic replay outputs, and assert the recreated logical rows match the original rows exactly.

- [x] **Step 4: Implement rebuild as an explicit disposable-store operation**

The rebuilder always creates a fresh schema and re-applies replay outputs in cursor order. It never attempts to recover authoritative state from SQLite.
- [x] **Step 5: Run projection tests and commit**

```powershell
dotnet test tests/Wolpertinger.Edge.Tests/Wolpertinger.Edge.Tests.csproj -c Release --filter FullyQualifiedName~Projections
git add src/Wolpertinger.Edge/Projections tests/Wolpertinger.Edge.Tests/Projections
git commit -m "feat: add rebuildable SQLite projections"
```

### Task 10: End-to-end runner and exact replay

**Files:**
- Create: `src/Wolpertinger.Edge/Runtime/VerticalSliceRunner.cs`
- Create: `src/Wolpertinger.Edge/Replay/ExactReplayRunner.cs`
- Create: `src/Wolpertinger.Edge/Diagnostics/DiagnosticEvent.cs`
- Modify: `src/Wolpertinger.Host/Program.cs`
- Create: `tests/Wolpertinger.Integration.Tests/FsdJumpVerticalSliceTests.cs`
- Create: `tests/Wolpertinger.Integration.Tests/ExactReplayTests.cs`

**Interfaces:**
- `VerticalSliceRunner.ProcessJournalLineAsync(ReadOnlyMemory<byte>, CancellationToken)` is the live/offline-ingest path.
- `ExactReplayRunner.RunAsync(...)` reads only the normalized ledger, drives fresh kernels, and returns final agreed state digest plus deterministic outputs.
- `Wolpertinger.Host` exposes only minimal `ingest` and `replay` commands for this slice.

- [x] **Step 1: Write a failing end-to-end integration test using the fixture**

The test creates a fresh temp data directory, builds/locates the real Ada kernel executable, processes the fixture line-by-line, and asserts exactly one authoritative `JumpCompleted` output after the FSDJump.
- [x] **Step 2: Implement the live/offline line-processing pipeline in the frozen order**

For each complete JSONL line execute only this sequence:

```text
append raw evidence + durable flush
-> classify / session-track / normalize
-> append durable normalization disposition or observation
-> if observation: Active+Shadow apply
-> if Applied: context decision -> CopilotOutput -> SQLite projection
```

A rejected/ignored normalization writes a diagnostic/disposition and stops for that raw record without consuming a kernel `EvidenceSequence`.

- [x] **Step 3: Write a failing exact-replay test**

After one successful ingest, save the final agreed digest and output list. Start a fresh pair of kernels with an empty authoritative state, read the normalized ledger from sequence 1, replay every dispatchable observation, and assert the final digest and deterministic output list equal the live run.

```csharp
Assert.Equal(live.FinalStateDigest, replay.FinalStateDigest);
Assert.Equal(live.Outputs, replay.Outputs);
```

- [x] **Step 4: Implement `ExactReplayRunner` without reading raw JSON or SQLite**

Replay consumes `IObservationReplaySource`, uses a fresh supervisor/kernel pair, passes each persisted observation unchanged, and feeds only newly `Applied` Active facts through the same `ContextDecisionEngine` and formatter.

- [x] **Step 5: Add the minimal headless CLI**

Support:

```text
wolpertinger-host ingest --journal <jsonl> --data <directory> --kernel <kernel-executable>
wolpertinger-host replay --data <directory> --kernel <kernel-executable>
```

Print `CopilotOutput.Text` to stdout and diagnostics to stderr. Do not add a CLI framework dependency.
- [x] **Step 6: Run the happy-path integration/replay tests and commit**

```powershell
dotnet test tests/Wolpertinger.Integration.Tests/Wolpertinger.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~FsdJumpVerticalSliceTests|FullyQualifiedName~ExactReplayTests"
git add src/Wolpertinger.Edge/Runtime src/Wolpertinger.Edge/Replay src/Wolpertinger.Edge/Diagnostics src/Wolpertinger.Host tests/Wolpertinger.Integration.Tests
git commit -m "feat: complete FSDJump vertical slice"
```

### Task 11: Failure matrix with real-process recovery

**Files:**
- Create: `tests/Wolpertinger.Integration.Tests/FailureSemanticsTests.cs`
- Create: `tests/Wolpertinger.Integration.Tests/KernelRecoveryTests.cs`
- Modify as defects require: only the component that fails its newly added test

**Interfaces:**
- Exercises the already-defined public/internal boundaries; this task must not introduce a second recovery architecture.

- [x] **Step 1: Add invalid-numeric evidence-retention test**

Feed an FSDJump whose `FuelLevel` coefficient overflows Int64. Assert the raw record exists and validates in the Evidence Log, a rejected-normalization diagnostic/disposition exists, the normalized observation sequence does not advance, and neither kernel state digest changes.

- [x] **Step 2: Add identity-conflict fail-closed test**

After binding synthetic FID `F100`, feed a deliberately crafted bound-context observation for `F200`. Assert `IdentityConflict`, no `JumpFact`, no output, and no canonical-state mutation.

- [x] **Step 3: Add sequence/integrity fault tests against the real kernel process**

Send cursor 3 while cursor 2 is expected and assert `SequenceGap`. Resend the last accepted cursor with a different evidence digest and assert `IntegrityFault`. In both cases the returned state digest must equal the pre-fault digest.
- [x] **Step 4: Add real Active-process failover/rejoin test**

Process `SessionBound`, kill the Active PID exposed through read-only supervisor diagnostics, then process the committed FSDJump observation. Assert the caught-up Shadow is promoted under a strictly larger epoch, produces the authoritative output, the killed kernel is restarted and replayed as Shadow, and both end on the same cursor/digest.

- [x] **Step 5: Add real Shadow-process restart/catch-up test**

Kill the Shadow after `SessionBound`, process FSDJump through the surviving Active, restart the Shadow at the current epoch, replay it to current cursor, and assert digest equality without an authority-epoch change.

- [x] **Step 6: Run the complete failure suite and fix only demonstrated defects**

```powershell
dotnet test tests/Wolpertinger.Integration.Tests/Wolpertinger.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~FailureSemantics|FullyQualifiedName~KernelRecovery"
```

Every bug fix must first reproduce as one failing test and then make that exact test pass. Do not add speculative recovery branches.

- [x] **Step 7: Commit failure/recovery coverage**

```powershell
git add tests/Wolpertinger.Integration.Tests src kernel
git commit -m "test: verify fail-closed recovery semantics"
```

### Task 12: Public documentation, CI gate, and final acceptance

**Files:**
- Modify: `README.md`
- Modify: `ROADMAP.md`
- Create: `.github/workflows/ci.yml`
- Create: `docs/architecture/vertical-slice-v0.md`
- Modify: `NOTICE.md`

**Interfaces:**
- Documents exactly what exists after this plan; no future feature is described as implemented.
- CI executes the same .NET tests, Ada runtime tests, SPARK proof, and full integration path required locally.
- [ ] **Step 1: Update public docs to match the architecture that now exists**

Change README foundation language to: `.NET 10 Edge/Core Host + narrow Ada/SPARK Trusted Kernel`. Keep the project-status wording factual. Update ROADMAP so FuE/foundation freeze are complete and the first FSDJump vertical slice is marked implemented only after all acceptance tests pass.

Create `docs/architecture/vertical-slice-v0.md` with the concrete process/data flow, offline fixture instructions, replay command, proof boundary, and explicit non-goals. Do not claim certification, HA, or whole-program formal verification.

Update `NOTICE.md` from its research-phase placeholder to the actual runtime dependency inventory: `System.Formats.Cbor 10.0.11` (MIT), `Microsoft.Data.Sqlite 10.0.11` (MIT), and `cbor_ada 0.3.0` at commit `b448c366117ff9f6c050b13d4fe609bb79495759` (Apache-2.0). State that each dependency retains its own licence; do not imply those components are relicensed under EUPL.

- [ ] **Step 2: Add one Windows CI job matching the supported development target**

Use:

```yaml
- uses: actions/checkout@v7
- uses: actions/setup-dotnet@v6
  with:
    dotnet-version: 10.0.111
- uses: alire-project/setup-alire@v6.0.0
  with:
    version: 2.1.1
    toolchain: gnat_native=16.1.0 gprbuild=26.0.1
```

Then non-interactively select `gnat_native=16.1.0` and `gprbuild=26.0.1`, build the kernel with the validation profile, run Ada tests/proofs, build .NET Release, and run all .NET tests including real-kernel integration tests.

- [ ] **Step 3: Run the complete local acceptance gate from a clean build**

```powershell
dotnet clean WOLPERTINGER.slnx -c Release
dotnet restore WOLPERTINGER.slnx
dotnet build WOLPERTINGER.slnx -c Release
dotnet test WOLPERTINGER.slnx -c Release
Push-Location kernel; alr build --validation; Pop-Location
Push-Location kernel/tests; alr build --validation; alr run; Pop-Location
Push-Location kernel/proof; alr exec -- gnatprove -P wolpertinger_kernel_proof.gpr --level=2 --report=all; Pop-Location
```
- [ ] **Step 4: Run the actual host once in ingest and replay mode**

```powershell
$kernel = (Resolve-Path 'kernel\bin\wolpertinger_kernel.exe').Path
$data = Join-Path $env:TEMP 'wolpertinger-v0-acceptance'
Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
dotnet run --project src/Wolpertinger.Host -c Release -- ingest --journal fixtures/journal/live-v4-fsdjump-session.jsonl --data $data --kernel $kernel
dotnet run --project src/Wolpertinger.Host -c Release -- replay --data $data --kernel $kernel
```

Expected: both commands succeed; each run emits the same one deterministic jump-output text, and replay reports the same final state digest as the ingest run.

- [ ] **Step 5: Run repository hygiene checks**

```powershell
git diff --check
git status --short
git grep -nEI '(api[_-]?key|client[_-]?secret|password|bearer[[:space:]]+[A-Za-z0-9_-]{16,})' -- ':!docs/superpowers/**'
```

Expected: no whitespace errors, only intended tracked changes before the documentation commit, and no credential-like material.

- [ ] **Step 6: Commit docs/CI and run the final gate again**

```powershell
git add README.md ROADMAP.md NOTICE.md .github/workflows/ci.yml docs/architecture/vertical-slice-v0.md
git commit -m "docs: document first vertical slice"
dotnet test WOLPERTINGER.slnx -c Release
Push-Location kernel/tests; alr run; Pop-Location
Push-Location kernel/proof; alr exec -- gnatprove -P wolpertinger_kernel_proof.gpr --level=2 --report=all; Pop-Location
git status --short
```

Expected: all tests/proofs pass and the worktree is clean.

## Acceptance traceability

| Foundation acceptance criterion | Implemented / verified by |
| --- | --- |
| Raw FSDJump evidence durable before processing | Tasks 3, 4, 10 |
| Versioned deterministic normalized observation | Tasks 2, 4 |
| Active + Shadow same input / same digest | Tasks 6, 7, 10 |
| Headless authoritative state transition | Tasks 5, 6 |
| Deterministic JumpFact / context / output | Task 8 |
| Exact replay reproduces state digest/output | Task 10 |
| Same observation resend is idempotent | Tasks 5, 11 |
| Numeric/identity/gap/integrity faults fail closed | Tasks 4, 5, 11 |
| Provenance/freshness are explicit in trusted FSDJump state/facts | Tasks 2, 4, 5, 6, 8 |
| Kernel restart catches up and rejoins Shadow | Tasks 7, 11 |
| SQLite can be deleted and rebuilt | Task 9 |
| SPARK proof boundary has zero unproved obligations | Tasks 5, 6, 12 |
| No network/LLM/UI dependency in the slice | Global constraints + Task 12 CI |

## Scope stop condition

When Task 12 passes, **Stage 1 is complete**. Do not expand the same branch into voice, UI, plugins, CAPI, EDDN, broader journal coverage, or a general Elite domain model. Open the next design/plan only for the next user-visible capability built on the proven slice.

If implementation reveals a concrete incompatibility with the approved foundation spec, stop at the failing task, record the evidence, and reopen only that specific design decision. Do not restart general FuE.
