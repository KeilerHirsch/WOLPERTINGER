# Stage 1 Kernel Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans task-by-task. Every behavior change is RED -> GREEN and every task ends with a reviewable gate.

**Goal:** Close the verified host-restart, pending-apply, fact-agreement, and state-digest gaps before UI or new product modules are built on the Stage-1 foundation.

**Architecture:** Keep the normalized ledger as the exact kernel replay source and raw evidence as the source for future reinterpretation. On host boot, start fresh Active/Shadow kernels, assign a fresh authority epoch, replay the normalized ledger to dual agreement, restore host identity from the durable SessionBound observation, and only then accept new ingest. If an ambiguous apply fails after ledger durability, stop ingest and require reopen/replay rather than advancing to the next cursor.

**Tech Stack:** .NET 10, Ada/SPARK, canonical CBOR, SHA-256, append-only normalized ledger, xUnit, GNATprove 16.1.

**Spec:** `docs/superpowers/specs/2026-09-06-wolpertinger-foundation-design.md`

## Global Constraints
- No new product features, CAPI, EDDN, voice, UI, or plugin work in this pass.
- Normalized ledger remains the exact replay truth for the typed kernel stream.
- Raw evidence remains append-only and is not replaced by snapshots.
- Only current-epoch ACTIVE output after Active/Shadow agreement may surface.
- EUPL-1.2-only for WOLPERTINGER-owned code.
- No reset, clean, force-push, or history rewrite.

---
### Task 1: Warm boot and identity recovery

**Files:**
- Modify: `src/Wolpertinger.Edge/Kernel/KernelSupervisor.cs`
- Modify: `src/Wolpertinger.Edge/Journal/SessionIdentityTracker.cs`
- Modify: `src/Wolpertinger.Edge/Runtime/VerticalSliceRunner.cs`
- Test: `tests/Wolpertinger.Integration.Tests/HostRestartRecoveryTests.cs`
- Test: `tests/Wolpertinger.Edge.Tests/Kernel/KernelSupervisorTests.cs`

**Interfaces:**
- `KernelSupervisor.StartAsync()` must reach the ledger head before returning when an `IObservationReplaySource` is configured.
- `SessionIdentityTracker.Restore(SessionBinding binding)` restores the durable SessionBound identity without synthesizing a new observation.

- [x] **Step 1:** Write an integration test: ingest the fixture, dispose the runner, reopen the same data directory, assert the last agreed cursor/digest are restored, then ingest a third FSDJump and verify it applies instead of `SequenceGap`.
- [x] **Step 2:** Run the focused integration test and verify RED on restart.
- [x] **Step 3:** Add a supervisor unit test proving `StartAsync()` replays configured observations to Active and Shadow and records the final agreed cursor/digest before becoming ready.
- [x] **Step 4:** Implement minimal startup replay plus identity restore from the last durable SessionBound observation.
- [x] **Step 5:** Run focused unit + integration tests to GREEN.
- [x] **Step 6:** Commit `fix: recover trusted authority on host restart`.

### Task 2: Close the ledger-before-apply crash window

**Files:**
- Modify: `src/Wolpertinger.Edge/Kernel/KernelSupervisor.cs`
- Modify: `src/Wolpertinger.Edge/Runtime/VerticalSliceRunner.cs`
- Test: `tests/Wolpertinger.Integration.Tests/HostRestartRecoveryTests.cs`

- [x] **Step 1:** Write RED integration test: bind session, kill both kernel processes, process the next FSDJump so raw+normalized records are durable but dispatch fails, verify the runner refuses any later ingest, dispose/reopen, and verify startup replay closes the pending cursor.
- [x] **Step 2:** Run and confirm failure is the missing fail-stop/recovery behavior.
- [x] **Step 3:** Add `KernelSupervisorLifecycle` (`Cold`, `Starting`, `Recovering`, `Synchronized`, `Degraded`, `Faulted`, `Stopped`) to diagnostics; transition to `Faulted` on ambiguous apply/recovery exceptions.
- [x] **Step 4:** Make `VerticalSliceRunner` stop accepting lines after any dispatch exception or non-committed kernel result. Reopen/replay is the only recovery path.
- [x] **Step 5:** Run focused tests to GREEN.
- [ ] **Step 6:** Commit `fix: close pending normalized work after restart`.
### Task 3: Make dual agreement cover the surfaced fact

**Files:**
- Modify: `src/Wolpertinger.Edge/Kernel/KernelSupervisor.cs`
- Test: `tests/Wolpertinger.Edge.Tests/Kernel/KernelSupervisorTests.cs`

- [x] **Step 1:** Add RED test where Active and Shadow return the same cursor/status/state digest but different `KernelJumpFact` values; expect `KernelDivergenceException` before publish.
- [x] **Step 2:** Run focused test and verify RED.
- [x] **Step 3:** Extend `ValidateAgreement` so null/non-null mismatch or record inequality in `JumpFact` is divergence.
- [x] **Step 4:** Run all supervisor tests to GREEN.
- [ ] **Step 5:** Commit `fix: require jump fact agreement`.

### Task 4: Digest every transition-relevant kernel field

**Files:**
- Modify: `kernel/src/wolpertinger_state_encoding.adb`
- Modify: contract/state golden fixtures under `fixtures/contracts/v1/`
- Modify: `tests/Wolpertinger.Kernel` Ada digest tests
- Modify: `tests/Wolpertinger.Edge.Tests/Contracts` golden expectations as needed
- Modify: `docs/architecture/vertical-slice-v0.md`

**Canonical change:** state identity advances to schema v2 and the array grows from 19 to 23 fields. Add `Has_Last_Cursor` before the cursor coordinates, add `Last_Message_Count` and `Last_Digest` after them, and encode `Fuel.Known` explicitly before fuel provenance/freshness. These values distinguish transition-relevant or authoritative states that must not share a digest.

- [x] **Step 1:** Add RED Ada tests proving states differing only in `Last_Message_Count` or only in `Last_Digest` produce different canonical bytes/digests.
- [x] **Step 2:** Run Ada tests and verify both REDs.
- [x] **Step 3:** Update the canonical encoder to schema v2 / 23 fields with neutral cursor/count/digest values when no last cursor exists and explicit `Fuel.Known` state.
- [x] **Step 4:** Independently regenerate state/response golden vectors (never from the Ada encoder), update expected SHA-256 values, and run Ada + .NET contract tests to GREEN.
- [x] **Step 5:** Update architecture docs with an explicit `included in digest / excluded from digest` statement.
- [x] **Step 6:** Run GNATprove; require all existing proof obligations to remain proved.
- [ ] **Step 7:** Commit `fix: cover transition state in canonical digest`.

### Task 5: Crash-window and lifecycle acceptance

**Files:**
- Test: `tests/Wolpertinger.Integration.Tests/HostRestartRecoveryTests.cs`
- Test: `tests/Wolpertinger.Integration.Tests/KernelRecoveryTests.cs`
- Modify: `docs/architecture/vertical-slice-v0.md`
- Modify: `README.md` / `ROADMAP.md` only if the closure status wording needs correction.

- [x] **Step 1:** Add/complete real-process tests for host restart with filled ledger, both-kernels-dead after ledger commit, Active failure/rejoin, and lifecycle state visibility.
- [x] **Step 2:** Verify request timeout remains the Stage-1 protocol-liveness detector; do not add a heartbeat message in this pass. Document periodic heartbeat as future observability work, not authority semantics.
- [x] **Step 3:** Run full .NET, Ada runtime, GNATprove, real ingest/replay, credential scan, and `git diff --check`.
- [ ] **Step 4:** Push normal commits, verify remote == local and Windows CI success.
- [ ] **Step 5:** Append a Notion closure checkpoint and reinstate the Foundation-complete status only after the remote gate is green.
