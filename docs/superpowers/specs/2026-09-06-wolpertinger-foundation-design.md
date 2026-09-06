# WOLPERTINGER Foundation Design

**Status:** Proposed architecture freeze for v0 foundation

**Project:** WOLPERTINGER — Wide-Area Operations & Logistics Platform for Exploration, Routing, Telemetry, Intelligence, Navigation, Guidance, Engineering, and Reconnaissance

**Product principle:** Your copilot should know what to do.

## 1. Scope

This document freezes the minimum architecture required to begin implementation without turning the project into an open-ended research exercise.

The first implementation target is one end-to-end vertical slice driven by a real `FSDJump` journal event.

The slice must prove ingestion, durable evidence, deterministic normalization, trusted state transition, derived fact generation, relevance evaluation, copilot output, and replay.

Anything not required to prove that path is deferred unless implementation reveals a concrete blocker.

## 2. Goals

- Keep authoritative game-state logic deterministic, replayable, and auditable.
- Keep volatile Frontier/community I/O outside the trusted state machine.
- Preserve original evidence so historical inputs can be reinterpreted later.
- Survive process failure without inventing or silently losing authoritative state.
- Make provenance, freshness, identity, and numeric semantics explicit.
- Keep the first implementation slice small enough to finish and verify.
## 3. Non-goals for the first slice

The first slice does not include:

- desktop UI or Avalonia;
- TTS, STT, or LLM integration;
- CAPI, EDDN, EDSM, Spansh, or other network/community sources;
- plugin execution;
- broad feature modules such as engineering, trade, mining, colonisation, or BGS;
- active/active kernel arbitration;
- hardware-level high availability;
- exhaustive modeling of every Frontier journal field;
- formal verification claims beyond properties actually proven by GNATprove.

## 4. System architecture

The frozen top-level flow is:

```text
Raw Sources
  -> .NET Edge Host
  -> Durable Evidence + Normalized Observation Ledger
  -> Versioned Typed Boundary
  -> SPARK Active + Shadow Kernels
  -> Authoritative State + Derived Facts
  -> .NET Core API / Context Engine
  -> Presentation / Voice / Plugins / Clients
```

The authoritative core must operate fully headless.
## 5. Process topology and trust boundary

### 5.1 .NET Edge Host

C# on .NET 10 LTS owns the volatile integration boundary:

- Frontier journal and mutable snapshot file ingestion;
- file watching, filesystem behavior, network/OAuth/reconnect work when later added;
- JSON decoding and schema-drift tolerance;
- raw evidence persistence;
- normalization into versioned typed observations;
- normalized observation ledger and monotonic sequencing;
- non-authoritative API/read-model work.

Raw JSON never enters the SPARK process.

### 5.2 Ada/SPARK Trusted Kernel

The Trusted Kernel starts after the typed boundary and owns:

- semantic invariant validation;
- ordering and idempotency;
- canonical authoritative state transitions;
- provenance/freshness semantics used by authoritative rules;
- deterministic derived facts and correctness-critical calculations.

The trusted computing base stays intentionally small. It has no direct UI, TTS, plugin, network, or arbitrary filesystem responsibility.
## 6. Kernel redundancy and supervisor

WOLPERTINGER runs two identical SPARK kernel processes in an active + hot-passive shadow topology.

Both kernels consume the same durable, sequenced typed observation stream and independently compute deterministic state.

Exactly one kernel holds the current monotonic authority epoch and may publish authoritative output.

The shadow kernel is side-effect-free. It remains current and reports its consumed sequence and deterministic state digest.

Design rule:

> **Two compute. One decides. One writes.**

The supervisor is a small control-plane component. It owns health/liveness policy, role assignment, authority epoch/fencing, divergence detection, restart coordination, and promotion.

It does not parse Frontier data, own canonical game state, or become the durable source of truth.

A stale kernel output carrying an old authority epoch must be rejected downstream. Promotion requires fencing the previous epoch first.

If active and shadow disagree on the same input sequence, automatic promotion is prohibited until the disagreement is resolved or the shadow is rebuilt. Two replicas can detect disagreement; they cannot determine which replica is correct by voting.

Same-machine redundancy is process fault containment only. It does not protect against OS, power, storage, or whole-machine failure.
## 7. Evidence and persistence

Persistence is deliberately split into two roles.

### 7.1 Append-only Evidence Log

The Evidence Log is the durable historical record. It stores the original source payload plus source metadata needed for replay, diagnosis, migration, and later reinterpretation.

The log is segmented and append-only. Raw records use explicit framing, integrity metadata, and a stable record identity/reference. Closed segments are immutable. The authoritative EvidenceSequence is assigned only after the raw record is durably committed.

A crash-truncated tail may be cut back only to the last fully valid record boundary. Mid-history corruption is reported as corruption and is never silently skipped.

A record is eligible for downstream authoritative processing only after its durable commit completes.

### 7.2 Normalized Observation Ledger

After raw evidence is committed, normalization produces the exact typed observation messages used by the kernel contract.

The normalized ledger is durable and append-only for the exact-replay history. It stores:

- `EvidenceSequence`;
- `MessageOrdinal` when one evidence record yields multiple bounded kernel messages;
- contract/schema version;
- normalizer version;
- numeric-semantics version;
- evidence reference/digest;
- canonical typed payload.

This supports exact replay independent of future parser changes while raw evidence supports deliberate reinterpretation through newer normalizers.
### 7.3 SQLite projections

SQLite is a rebuildable projection/read-model store, not the historical source of truth.

It may hold indexes, diagnostics, query-oriented state, snapshots/checkpoints, and UI/read-model data.

Deleting SQLite must not destroy replay capability. Rebuilding it from durable evidence/normalized observations is a required recovery path.

Persistence remains single-writer behind the authority boundary; the passive kernel never performs authoritative database writes.

## 8. Sequence, commit, and delivery semantics

The order is frozen:

```text
raw input
  -> durable raw-evidence commit
  -> assign/confirm EvidenceSequence
  -> normalize
  -> durable normalized-ledger commit
  -> dispatch same sequenced message(s) to both kernels
```

The authoritative delivery cursor is `{EvidenceSequence, MessageOrdinal}`.

Resending the same cursor with the same evidence/payload digest is idempotent and returns ACK/no-op after prior application.

Receiving the same cursor with a different digest is an integrity fault.

Receiving a future cursor with a gap causes replay/resynchronization from the first missing cursor; the kernel does not guess the missing transition.

A durably committed raw record that has no normalized-ledger entry after a crash is pending work, not lost work. Recovery resumes such records in raw append order and assigns the next EvidenceSequence exactly once through the normalized ledger.
## 9. Versioned trusted contract

The logical wire contract is transport-neutral and versioned independently from the physical local IPC transport.

The initial contract format is:

- explicit outer length framing;
- deterministic CBOR encoding;
- CDDL-defined message schemas;
- bounded message size and nesting;
- definite lengths;
- no duplicate map keys;
- valid UTF-8 only;
- no arbitrary CBOR tags;
- stable integer field identifiers.

Unknown external Frontier fields/events are tolerated and preserved as raw evidence. Unknown internal trusted message kinds are rejected as protocol/version errors.

A kernel observation envelope carries, at minimum:

- protocol/schema version;
- `{EvidenceSequence, MessageOrdinal}`;
- evidence digest/reference;
- profile/session identity context;
- observation kind;
- typed normalized payload;
- relevant source/observation/commit time metadata.

The exact physical IPC transport is deferred until implementation planning. Named pipes/UDS/local transport choices must not alter protocol semantics.
## 10. Identity and session semantics

Commander identity is not the display name.

The authoritative profile key is:

```text
ProfileKey = { FID, GalaxyRealm }
```

`GalaxyRealm` distinguishes at least `LIVE`, `LEGACY`, `BETA_OR_PTS`, and `UNKNOWN`.

`GameExperience` is separate from realm and distinguishes Horizons/Odyssey context where known. `Odyssey == false` must never be used as a synonym for Legacy.

A save reset starts a new `SaveEpoch` under the same profile key so historical pre-reset state cannot bleed into the new save.

A logical game session is independent of journal file rotation/part number.

Session lifecycle states are:

```text
UNBOUND -> IDENTITY_PENDING -> ACTIVE
ACTIVE -> CLEAN_END | ABRUPT_END | CONFLICTED
```

Identity or realm ambiguity is fail-closed for authoritative mutation. Raw evidence is still retained while identity is pending or conflicted.

A mid-session FID/realm conflict never triggers a clever implicit migration into another profile.
## 11. Time semantics

Four clocks are distinct:

- `SourceTimeUtc` — timestamp asserted by Frontier/source;
- `ObservedUtc` — when WOLPERTINGER observed the input;
- `CommitUtc` — when durable evidence commit completed;
- `MonotonicTick` — local monotonic measurement for timeout/latency/watchdog use; diagnostic only and excluded from authoritative replay/digests.

None of these clocks defines authoritative causal order. Causal processing order comes from the durable evidence sequence and message ordinal.

## 12. Numeric semantics

Normal floating-point values do not cross the Trusted Kernel boundary.

The trusted numeric model uses:

1. semantic scaled integers for common bounded domains where unit and precision are explicitly defined; and
2. bounded exact-decimal values represented as an integer coefficient plus bounded base-10 exponent for scientific/less-common values.

Raw lexical source numbers remain in evidence. Exact-decimal values have one canonical representation: zero is {coefficient=0, exponent10=0}; non-zero values remove redundant base-10 trailing zeros from the coefficient while adjusting the exponent within its declared bounds.

The numeric-semantics catalogue is versioned. Normalized observations record the catalogue version used.

No silent rounding, clamping, overflow wrapping, or `double` round-trip is permitted in the authoritative path.

If a numeric value cannot be represented exactly under the applicable semantic rule, the affected transition is rejected and diagnosed; unrelated domains continue where safe.
## 13. Provenance and freshness

Canonical facts carry explicit provenance and freshness/conflict state rather than collapsing all sources into one anonymous value.

The exact public/internal enum may evolve, but the architecture must support at least local journal/status observations, Frontier API data, community data, derived facts, user-entered facts, unknown/stale values, and conflicts.

Source precedence is domain/field specific. There is no single global rule that one source always beats every other source.

## 14. Deterministic state digest

Active/shadow comparison never hashes raw memory layouts.

Each kernel produces a canonical representation of authoritative state using a versioned deterministic serialization, then hashes that representation with SHA-256.

The digest input excludes nondeterministic process details such as memory addresses, wall-clock receive timing, logging metadata, and process IDs.

For the same contract version, same starting checkpoint, and same ordered observation stream, both kernels and replay must produce the same state digest.

## 15. Derived facts and context decisions

The kernel produces deterministic facts, not prose personality.

For the first slice, an `FSDJump` transition produces a typed `JumpFact` containing only validated authoritative information required by downstream relevance logic.

The .NET context layer decides whether the fact should be surfaced based on deterministic context/policy inputs.

Optional future AI may rewrite/explain an already established fact, but it never becomes the source of the fact or directly mutates canonical game state.
## 16. First implementation vertical slice

The first retained implementation proves exactly this path:

```text
real FSDJump journal line
  -> durable raw evidence
  -> deterministic .NET normalization
  -> durable normalized observation
  -> bounded versioned CBOR message
  -> SPARK Active + Shadow
  -> canonical location/fuel transition
  -> JumpFact
  -> deterministic context decision
  -> plain copilot output event
  -> replay with identical authoritative state digest
```

The first `FSDJump` normalization may use only the fields required to prove the architecture, initially including destination system identity/address, position where needed, jump distance, fuel used, and remaining fuel.

Fields not needed by the slice remain preserved in raw evidence and do not need corresponding SPARK types yet.

The first output is a headless structured/text event suitable for tests and CLI diagnostics. Building a graphical or spoken presentation is outside this slice.

## 17. Failure semantics

- Unknown Frontier input: preserve raw evidence; classify/ignore without crashing ingestion.
- Invalid internal contract: reject before authoritative mutation.
- Numeric representation failure: retain evidence, reject affected transition, emit explicit diagnostic.
- Identity ambiguity/conflict: retain evidence, block authoritative mutation for the affected profile/session.
- Sequence gap: request/replay from first missing cursor.
- Same cursor/different digest: integrity fault.
- Active/shadow digest disagreement: report critical divergence and prohibit automatic promotion until resynchronized or diagnosed.
- Kernel crash: supervisor restarts it; snapshot/checkpoint plus ledger replay catches it up before it can rejoin as shadow.
- Supervisor crash: external service/watchdog may restart it; absence of valid authority/fencing must fail safe rather than permit two authorities.
- SQLite loss/corruption: rebuild from durable evidence/normalized ledger.
- Abrupt game shutdown: session closes as abrupt/unclean, not fabricated as clean.

The default principle is controlled degradation over creative recovery.

## 18. Replay and recovery

Replay uses the same normalization/contract/state-transition semantics as live processing.

Two replay modes are supported conceptually:

- **Exact replay:** feed the persisted normalized observation ledger through the kernel contract used at the time.
- **Reinterpretation:** feed preserved raw evidence through a newer normalizer/catalogue to deliberately migrate or compare behavior.

A restarted kernel must be able to load a compatible checkpoint/snapshot, verify its cursor/version, and replay remaining normalized observations deterministically.

Checkpointing is an optimization; durable evidence and replayability are the correctness foundation.

Recovery targets are engineering objectives to measure, not guarantees: retain all committed evidence and recover an isolated kernel-process failure fast enough to avoid meaningful commander disruption.

## 19. Verification strategy

The first slice requires automated tests at every boundary rather than relying on end-to-end happy-path tests alone.
Required verification includes:

- raw evidence append/recovery tests, including truncated-tail recovery;
- normalization golden tests from representative `FSDJump` lines;
- cross-language CBOR golden vectors;
- malformed/oversized protocol input rejection;
- SPARK contract/invariant tests and GNATprove for code inside the declared SPARK proof boundary;
- idempotent resend tests;
- sequence-gap and digest-conflict tests;
- Active/Shadow same-input/same-digest tests;
- replay-to-identical-state-digest tests;
- kernel restart/catch-up tests;
- SQLite deletion/rebuild test;
- identity-pending/conflict tests;
- exact numeric conversion and rejection tests.

No test may depend on an LLM or external network service for the first slice.

## 20. Dependency and supply-chain rules

The initial architecture does not freeze a third-party Ada CBOR package merely because one appears promising.

Any parser/serialization/crypto dependency entering the trusted boundary must be pinned, license-compatible with EUPL-1.2 distribution, reproducibly built, and independently tested against the contract vectors.

Where a dependency claims SPARK proof coverage, WOLPERTINGER must reproduce the relevant GNATprove run before treating that claim as part of the trust argument.

The exact SHA-256 and CBOR implementations remain implementation choices subject to this audit.

## 21. Privacy and support evidence

Elite journal data can contain commander identity, location, and activity history. Local-first remains the default.

Diagnostic/support bundles must be designed for explicit export and redaction; no automatic telemetry or upload is introduced by this foundation.
## 22. Deferred decisions

These are intentionally not blockers for starting the first slice:

- Avalonia or any final desktop UI framework;
- TTS/STT/LLM providers and personality system;
- plugin sandbox/capability model;
- CAPI/EDDN/community-source integration;
- exact public provenance enum names;
- final per-domain numeric scale catalogue beyond fields required by `FSDJump`;
- final IPC transport choice, provided the versioned logical contract remains unchanged;
- hardware/LAN failover;
- full retention/archive policy;
- final update/installer strategy.

A deferred item becomes new FuE only when implementation reaches it and a concrete unresolved question blocks progress.

## 23. First-slice acceptance criteria

The architecture is proven when all of the following are true:

1. A real valid `FSDJump` journal event is durably retained before authoritative processing.
2. Its normalized observation is deterministic, versioned, and durably replayable.
3. Active and shadow SPARK kernels process the same cursor and produce the same authoritative state digest.
4. The authoritative state reflects the jump without depending on UI, network, plugin, TTS, or LLM code.
5. A deterministic `JumpFact` and deterministic relevance/output event are produced.
6. Replaying the retained input from a clean state reproduces the same authoritative state and digest.
7. Resending the same observation is idempotent.
8. Invalid numerics, identity conflicts, sequence gaps, and integrity conflicts fail closed without losing raw evidence.
9. A restarted kernel can catch up and rejoin as shadow.
10. SQLite can be removed and rebuilt without losing historical truth.
## 24. Scope guard

WOLPERTINGER is deliberately using reliability engineering ideas without turning a game companion into a certification programme.

The implementation should prefer the smallest design that satisfies the frozen invariants. New daemons, consensus algorithms, distributed databases, proof obligations, or abstraction layers require a concrete failure mode or product requirement before they are added.

The working rule is:

> **Simple by default. Powerful by choice. Slightly unhinged by design.**

The trusted boundary exists to make correctness-critical behavior easier to reason about, not to make every line of the product formally verified.

## 25. Documentation consistency note

The current public README predates the frozen Ada/SPARK trusted-kernel decision and describes the language/runtime foundation only as C#/.NET 10.

After this design is approved, public foundation documentation should be amended to state the split accurately: C#/.NET 10 for the Edge/Core Host and Ada/SPARK for the narrow Trusted Kernel.

That documentation correction is part of the implementation-plan preparation, not a reason to reopen foundation FuE.

## 26. Freeze statement

Upon approval of this document, general v0 foundation FuE is closed.

Implementation planning may investigate narrow questions required to choose concrete libraries, toolchain versions, local IPC transport, numeric scales, or test fixtures, but it must not reopen the architecture unless new evidence demonstrates a specific incompatibility with this design.