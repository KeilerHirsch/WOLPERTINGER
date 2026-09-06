# WOLPERTINGER Roadmap

This roadmap is intentionally high-level. WOLPERTINGER will not accumulate implementation before the underlying product, data, legal, and architecture decisions have been researched and frozen.

## Phase 0 — Public foundation

- Establish project identity, scope, licence, and clean-room boundary.
- Publish the architectural intent without pretending unfinished features exist.
- Collect early technical and commander feedback.

## Phase 1 — Research & reconnaissance

- Map Frontier-supported and documented data surfaces.
- Audit relevant Elite Dangerous community tooling and interoperability patterns.
- Build a feature and user-need matrix across exploration, navigation, logistics, engineering, colonisation, telemetry, and voice assistance.
- Verify data provenance, licensing, trademarks, privacy, and redistribution constraints.
- Evaluate desktop UI, audio/STT/TTS, networking, persistence, plugins, update delivery, Linux/Steam Deck, and support diagnostics.

## Phase 2 — Product & architecture freeze

- Canonical game-state model.
- Data-source adapters and validation contracts.
- Event and derived-fact model.
- Provenance and freshness semantics.
- Rules and recommendation engine.
- Voice/assistant pipeline and AI trust boundary.
- Persistence, replay, plugin, and presentation contracts.
- UX information architecture and progressive-disclosure model.

## Phase 3 — Implementation

Implementation begins only after the foundation has been researched and reviewed.

The first vertical slice should prove the architecture end to end:

`Elite event → validated input → canonical state → derived fact → relevance decision → copilot output → replay test`

Later capability modules can then grow on the same foundation, including:

- Copilot and voice interaction
- Exploration and exobiology
- Navigation and expedition support
- Engineering and material planning
- Logistics, trading, mining, and fleet-carrier workflows
- Colonisation support
- Community extensions through a stable plugin surface

No dates or feature promises are attached yet. Research gets to change the plan before code makes changes expensive.
