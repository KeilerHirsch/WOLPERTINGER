# WOLPERTINGER Roadmap

This roadmap is intentionally high-level. WOLPERTINGER closes reviewed foundations before broad feature coverage.

## Stage 1 — Trusted foundation

**Complete.**

Retained path:

`Elite journal → durable raw evidence → deterministic normalized ledger → Active + Shadow trusted state → JumpFact → deterministic output → rebuildable projection → exact replay`

The foundation includes bounded CBOR/CDDL contracts, authority epoch/fencing, fail-closed identity and sequence semantics, canonical state digests, real-process kernel recovery, runtime tests and a SPARK proof boundary.

## Stage 2 — Presentation foundation

**In progress.**

Already on `main`:

- typed presentation contracts and validation;
- trusted fact projection into presentation snapshots;
- bounded latest-state publishing;
- local named-pipe transport;
- neutral presentation state/ViewModels;
- Windows-facing display, placement and surface infrastructure.

Remaining work is focused on the commander-facing tray/overlay/hub experience, reconnect/recovery behaviour and making the implemented presentation path boring to operate.

## Later capability stages

After the presentation path is stable, broader capability modules may grow on the same foundation:

- passive copilot / TTS;
- exploration and exobiology;
- navigation and expedition support;
- engineering and material planning;
- logistics, trading, mining and fleet-carrier workflows;
- colonisation support;
- optional external providers and community extensions.

These are directions, not release promises. Optional services must not become authoritative game state.
