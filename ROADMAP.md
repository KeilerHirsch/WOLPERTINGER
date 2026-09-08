# WOLPERTINGER Roadmap

This roadmap is intentionally high-level. WOLPERTINGER builds reviewed foundations before broad feature coverage.

## Foundation research and architecture freeze — complete

The initial research, threat/failure modelling, source-boundary review, licence choice, deterministic data contract, replay model, and `.NET 10 + Ada/SPARK` process architecture are frozen for the first vertical slice.

## Stage 1 — FSDJump foundation slice

**Complete.** The first retained FSDJump vertical slice is implemented and has passed the local foundation acceptance gate. The Windows CI workflow reproduces the build/test/proof gate on pushes and pull requests.

Implemented scope:

`Elite journal → durable raw evidence → deterministic normalized ledger → ACTIVE+SHADOW trusted state → JumpFact → deterministic output → rebuildable projection → exact replay`

The slice also includes monotonic authority epochs, fencing, real-process Active/Shadow recovery, fail-closed numeric/identity/sequence/integrity semantics, canonical state digests, and a bounded SPARK proof boundary.

## Next capability stages

Only after Stage 1 closes will user-visible capability modules grow on this foundation, potentially including:

- Copilot and optional voice interaction
- Exploration and exobiology
- Navigation and expedition support
- Engineering and material planning
- Logistics, trading, mining, and fleet-carrier workflows
- Colonisation support
- Community extensions through a stable plugin surface

These are directions, not release promises. UI, CAPI, EDDN, voice, plugins, and broader journal coverage are explicitly outside the Stage-1 branch.
