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
        +---------- cursor + digest ----+
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

Only the fenced `ACTIVE` result may be surfaced, and only after the `ACTIVE` and `SHADOW` results agree on cursor, status, and canonical state digest during normal healthy operation.

## Trusted kernel

The trusted process receives bounded, versioned CBOR observations. Raw JSON, network responses, UI state, SQLite, plugins, speech, and AI do not enter the kernel process.

The SPARK proof boundary contains the bounded text helpers, domain state, facts, transition engine, and monotonic role/epoch control. CBOR, SHA-256 wrappers, stdio, and third-party `cbor_ada` are outside the formal proof boundary and are covered by runtime, golden-vector, framing, and process tests.

The current proof gate reports all proof obligations in that boundary as proved. This is not a claim that the complete application or third-party dependencies are formally verified.

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

## Recovery

Authority epochs are stored in an append-only, SHA-256-protected control log. Promotion requires a strictly larger epoch. A stale process therefore cannot become an authoritative publisher merely because it is still alive.

If the Shadow process fails, the Active continues processing and a new Shadow is rebuilt from normalized-observation replay at the current epoch. If the Active process fails after an agreed state exists, the caught-up Shadow is promoted under a fresh epoch and the replacement process rejoins as Shadow through replay.

Both-process loss, replay mismatch, cursor divergence, digest divergence, identity conflict, integrity faults, sequence gaps, and unrepresentable trusted numeric input fail closed.

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
