# FSDJump vertical slice v0

This document describes the first implemented WOLPERTINGER foundation slice. It is deliberately narrow: one local Elite Dangerous journal session is reduced to a deterministic `FSDJump` fact, output, projection, and exact replay result.

It does **not** claim certification, whole-program formal verification, high availability, or broad Elite Dangerous feature coverage.

## Process boundary

```text
Elite journal JSONL
        |
        v
.NET 10 Edge/Core Host
  durable raw Evidence Log
  -> session/realm tracking
  -> deterministic lexical normalization
  -> durable Normalized Observation Ledger
        |
        +-------------------------------+
        |                               |
        v                               v
Ada/SPARK kernel A                Ada/SPARK kernel B
   ACTIVE @ epoch N                 SHADOW @ epoch N
        |                               |
        +--- cursor + status + digest + fact ---+
                       |
                       v
               .NET supervisor
          agreement + fencing gate
                       |
                       v
                 JumpFact
                       |
            deterministic context
                       |
                 CopilotOutput
                       |
              SQLite projection
```

Only the fenced `ACTIVE` result may be surfaced, and only after the `ACTIVE` and `SHADOW` results agree on cursor, status, canonical state digest, and the complete surfaced `JumpFact`. The same fact-agreement rule is enforced when a replacement `SHADOW` rejoins after a degraded single-kernel interval.

## Trusted kernel

The trusted process receives bounded, versioned CBOR observations. Raw JSON, network responses, UI state, SQLite, plugins, speech, and AI do not enter the kernel process.

The SPARK proof boundary contains the bounded text helpers, domain state, facts, transition engine, and monotonic role/epoch control. CBOR, SHA-256 wrappers, stdio, and third-party `cbor_ada` are outside the formal proof boundary and are covered by runtime, golden-vector, framing, and process tests.

The current proof gate reports all proof obligations in that boundary as proved. This is not a claim that the complete application or third-party dependencies are formally verified.

## Canonical state identity

State identity uses a fixed 23-element canonical CBOR array with internal schema version `2`. The digest includes every semantically active field retained by `Kernel_State`: binding/profile/session identity; `Has_Last_Cursor`, cursor coordinates, message count, and last evidence digest; location-known plus active location value/provenance/freshness; fuel-known plus active fuel value/provenance/freshness; and last jump distance. Inactive payloads behind `Bound`, `Has_Last_Cursor`, `Location.Known`, or `Fuel.Known` are encoded as neutral values so hidden stale storage cannot change semantic identity.

The full redundant `Last_Jump` payload is not retained in kernel state; location and fuel are authoritative once, with only `Last_Jump_Distance` retained separately. Kernel role and authority epoch are deliberately excluded from the domain-state digest because they are control-plane state and are fenced independently on every response. Process IDs, supervisor lifecycle, SQLite projections, UI state, and outputs are also outside kernel-state identity.

## Evidence and replay

The live path is fixed:

```text
append raw evidence + flush-to-disk
-> classify / bind identity / normalize
-> append normalization disposition or canonical observation + flush-to-disk
-> apply identical observation to ACTIVE + SHADOW
-> compare fenced results
-> derive deterministic output
-> update disposable SQLite read model
```

Rejected or ignored raw records do not consume a trusted `EvidenceSequence`.

Exact replay reads the normalized ledger only. It does not read raw JSON or SQLite. Fresh kernel processes receive the persisted canonical observations in cursor order and must reproduce the same final canonical state digest and deterministic outputs.

A normal host start uses that same recovery rule before accepting new journal input: a fresh authority epoch is allocated, fresh ACTIVE/SHADOW processes are assigned, the normalized ledger is replayed to dual agreement, and the durable `SessionBound` identity is restored. Only then does supervisor lifecycle become `Synchronized` and ingest open.

Once a canonical observation is durable in the normalized ledger, dispatch is pending authority work until a committed dual result exists. Any ambiguous apply exception or agreed non-committed status moves the supervisor to `Faulted`; the runner rejects later journal lines before raw/normalized append. Reopen plus ledger replay is the Stage-1 recovery path for that crash window.

## Recovery

Authority epochs are stored in an append-only, SHA-256-protected control log. Promotion requires a strictly larger epoch. A stale process therefore cannot become an authoritative publisher merely because it is still alive.

If the Shadow process fails, supervisor lifecycle temporarily becomes `Degraded`: the fenced Active may apply the already-durable observation, then a replacement Shadow is rebuilt from normalized-observation replay at the current epoch. The result is not surfaced until the replacement reaches the same cursor/state digest and its surfaced `JumpFact` agrees with the Active result. If the Active process fails after an agreed state exists, the caught-up Shadow is promoted under a fresh epoch and the replacement process rejoins as Shadow through replay before the next observation is dispatched.

The Stage-1 liveness detector is the bounded request timeout on real kernel exchanges; `HasExited == false` alone is not authority readiness. No periodic heartbeat message is part of authority semantics in v0. Full Shadow rebuild currently replays the normalized ledger from the beginning and is therefore O(n) in session history; verified checkpoints are future performance work, not a second truth source.

Both-process loss, replay mismatch, cursor divergence, digest divergence, fact divergence, identity conflict, integrity faults, sequence gaps, and unrepresentable trusted numeric input fail closed. Supervisor lifecycle exposes `Cold`, `Starting`, `Recovering`, `Synchronized`, `Degraded`, `Faulted`, and `Stopped`; only `Synchronized` accepts new ingest.

## Offline fixture

The checked-in fixture is:

```text
fixtures/journal/live-v4-fsdjump-session.jsonl
```

After building the kernel, run:

```powershell
$kernel = (Resolve-Path 'kernel\bin\wolpertinger_kernel_main.exe').Path
$data = Join-Path $env:TEMP 'wolpertinger-v0'

dotnet run --project src/Wolpertinger.Host -c Release -- ingest `
  --journal fixtures/journal/live-v4-fsdjump-session.jsonl `
  --data $data `
  --kernel $kernel

dotnet run --project src/Wolpertinger.Host -c Release -- replay `
  --data $data `
  --kernel $kernel
```

Both commands emit the same deterministic jump text. Their diagnostic `StateDigest` must also match.

## Deliberate non-goals of v0

- no UI
- no voice/TTS/STT
- no LLM in the authoritative path
- no Frontier CAPI or EDDN integration
- no plugin execution
- no general Elite domain model
- no broad journal-event coverage
- no release/update system

Those capabilities can be layered later without changing the rule that volatile inputs remain outside the trusted kernel and authoritative state is recoverable from durable canonical observations.
