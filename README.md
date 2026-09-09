# WOLPERTINGER

**Wide-Area Operations & Logistics Platform for Exploration, Routing, Telemetry, Intelligence, Navigation, Guidance, Engineering, and Reconnaissance**

*Your copilot should know what to do.*

**High-assurance inside. KISS outside.**

WOLPERTINGER is an open-source, local-first companion platform for **Elite Dangerous**. One authoritative core turns game context into the information that matters now, then presents it through the surface that fits the moment instead of making the commander coordinate a pile of apps, tabs, overlays, spreadsheets, providers, and startup order.

> **Project status: Pre-alpha — trusted foundation complete, presentation subsystem in progress**
> **Stage 1 is complete:** the retained `FSDJump` path is implemented end to end through durable evidence, deterministic state, trusted Ada/SPARK processing, and replay.
> **Presentation foundation is now on `main`:** typed presentation contracts, trusted fact projection, bounded snapshot publishing, and local process transport are implemented. Commander-facing tray/overlay/hub surfaces are still under active development.
> There is **no supported end-user release yet**.

## The 30-second version

**What does this give me as a commander?**
Context instead of a feature wall. WOLPERTINGER is designed to notice what you are doing — jumping, scanning, engineering, mining, docking, colonising — and bring forward the useful facts and next actions without making you hunt through unrelated panels.

**How much do I have to configure?**
The Release-1 target is **useful before configured**: local Journal/Status value without provider accounts or API keys, automatic detection where safe, and as close to **zero mandatory clicks during normal play** as the platform allows. Optional integrations add capability; they do not unlock the basic product.

**Why is that better than another tool stack?**
Because the commander should not be the integration bus. **WOLPERTINGER is not trying to replace 14 Elite tools. It is trying to stop the commander from having to coordinate 14 Elite tools.**

<p align="center">
  <img src="docs/assets/wolpertinger-concept-ui.webp" alt="WOLPERTINGER concept UI showing route, exploration, engineering, and shipboard-assistant context in an Elite Dangerous cockpit" width="100%">
</p>

<p align="center"><sub><strong>Concept UI — early development.</strong> This image communicates product direction and intended workflow; it is not a screenshot of implemented feature status.</sub></p>

## Commander experience

The intended normal path is deliberately boring:

`Install → launch → CMDR detected → useful context appears → optional connections → play`

That is a product requirement, not marketing decoration.

- **Useful before configured.** Ask only when ambiguity cannot be resolved safely.
- **Context, not panel archaeology.** Relevant information comes forward with the current task.
- **One core — multiple presentation surfaces.** Tray, docks/overlays, compact views, fullscreen hub, later voice, and diagnostics all consume the same presentation truth.
- **No orchestration tax.** Normal play should not depend on browser tabs, spreadsheet hand-offs, plugin startup order, or copying values between tools.
- **Optional means optional.** External providers, community services, cloud features, and future AI/personality layers must degrade without taking local authoritative state down with them.
- **Passive copilot help without command setup.** Useful TTS/callouts are a separate capability from STT/voice commands; VoiceAttack-style command configuration must never be a prerequisite for receiving useful information.
- **Predictable surfaces.** Presentation may adapt to context, but it must not surprise the commander, steal focus, or become the owner of game truth.

## One core — multiple presentation surfaces

No presentation surface owns authoritative state. The same accepted facts and decisions can be shown at different densities without being recalculated independently:

- **Tray** for presence, health, and recovery entry points.
- **Overlay / dock surfaces** for low-friction flight context.
- **Compact / standard / expanded views** for changing information density.
- **Fullscreen Hub** when the commander wants the deep workspace.
- **Voice / TTS later** for passive, context-aware assistance.
- **Diagnostics / SYS** for provenance, freshness, transport, evidence, replay, and the machinery normal users should not have to stare at.

The presentation process is deliberately disposable. If a UI surface dies, the authoritative core keeps its state; the surface should restart and resnapshot instead of forcing Elite or the trusted kernel to restart.

## Fail without becoming another chore

A mature companion is judged by the ugly path too. WOLPERTINGER's UX rule is: **say what happened, say what still works, offer one clear recovery action.**

Target behaviour looks like this:

> External market data is unavailable — using cached data from 18 minutes ago. Your local game state is unaffected.

Not this:

> `ProviderAdapterException: HTTP 429`

Optional provider/auth/voice/overlay failures should degrade visibly rather than block the local product. Stale data stays labelled stale; uncertainty stays uncertainty; raw implementation errors belong in diagnostics.

## Settings are not a feature attic

Settings configure **behaviour**: voice on/off, overlay preferences, density, optional provider connections, privacy, and other genuinely cross-cutting choices.

Domain actions stay where the commander expects them. Engineering actions belong with Engineering. Route actions belong with Navigation. Source freshness belongs with the fact it qualifies. Generic Settings must not become the place where features go to hide.

Advanced controls, raw evidence, transport state, provider internals, replay, and recovery tooling still exist — deliberately — in the **Nerd Basement**.

## What exists today

Implemented and merged on `main`:

- durable raw evidence and normalized observation ledger;
- bounded deterministic CBOR/CDDL trusted contract;
- Ada/SPARK Active + hot-passive Shadow trusted kernels;
- epoch/fencing, deterministic replay, canonical state digest, and rebuildable projections;
- retained `FSDJump → JumpFact → deterministic context/output` vertical slice;
- typed Presentation Contract and validation;
- trusted JumpFact projection into presentation snapshots;
- bounded latest-state publisher and current-user local named-pipe transport.

Still in active development: neutral presentation state/ViewModels, Windows display/DPI/topology policy, overlay/tray/fullscreen surfaces, reconnect lifecycle, passive voice/TTS, optional external providers, and broader Elite domain workflows.

## Quick start — contributors

WOLPERTINGER is currently for contributors and architecture review, not end-user installation.

```powershell
git clone https://github.com/KeilerHirsch/WOLPERTINGER.git
cd WOLPERTINGER
dotnet --version
```

The pinned .NET SDK is **10.0.111**. The trusted-kernel toolchain uses Ada 2022 / SPARK and Alire; exact implementation and verification details live in the committed plans rather than in a first-run wall of text.

## Architecture in 30 seconds

```text
Elite Journal / Status / future optional sources
        |
        v
.NET 10 Edge Host
  - volatile I/O + schema drift handling
  - append-only raw evidence
  - deterministic normalization + normalized ledger
        |
        v
bounded, versioned CBOR/CDDL trusted contract
        |
        v
Ada/SPARK Trusted Kernel x2
  - Active + hot-passive Shadow
  - canonical authoritative state
  - deterministic facts + state digest
        |
        v
.NET context/output layer
        |
        v
Presentation Contract / snapshot transport
        |
        +--> Tray / Overlay / Docks
        +--> Fullscreen Hub
        +--> Diagnostics
        +--> later passive Voice/TTS
```

The headless authoritative path is intentionally isolated from UI, external services, plugins, TTS/STT, and LLMs. Presentation consumes trusted facts; it does not manufacture them. Optional integrations enter through adapters and carry explicit provenance/freshness instead of silently becoming truth.

**Development tooling disclosure:** AI-assisted tools may be used during research, review, documentation, and implementation. They are development aids, not runtime authorities. The trusted path remains local, deterministic, replayable, and independent of cloud/LLM availability.

## Why the inside is deliberately serious

The commander should not need to care about most of this, but the project does:

- **Edge/Core Host:** C# / .NET 10
- **Trusted Kernel:** Ada 2022 / SPARK, isolated process boundary
- **Kernel topology:** Active + hot-passive Shadow with supervisor epoch/fencing
- **Historical truth:** append-only raw evidence + append-only normalized ledger
- **Trusted contract:** bounded deterministic CBOR with CDDL schema
- **Replay:** the same ordered observations reproduce the same authoritative state digest
- **Identity/session semantics:** explicit and fail-closed
- **AI boundary:** optional explanation/personality only; never authoritative game state
- **WOLPERTINGER-owned code licence:** EUPL-1.2 only

**Complexity belongs inside the machine, not in the commander's workload.** The high-assurance architecture exists to make the outside calmer, more predictable, and easier to recover — not to turn normal play into systems engineering.

## Deep docs

- [Foundation design](docs/superpowers/specs/2026-09-06-wolpertinger-foundation-design.md)
- [Implemented Stage-1 vertical-slice architecture](docs/architecture/vertical-slice-v0.md)
- [Presentation subsystem design](docs/superpowers/specs/2026-09-09-wolpertinger-presentation-subsystem-design.md)
- [Presentation vertical-slice plan](docs/superpowers/plans/2026-09-09-wolpertinger-presentation-vertical-slice.md)
- [Roadmap](ROADMAP.md)

## Support the project

If WOLPERTINGER becomes useful to you, GitHub's **Sponsor this project** surface links to the project's configured funding target. Funding is voluntary and gives **no feature, ranking, review, release, roadmap, support, or technical-influence privileges**.

## Licence

WOLPERTINGER-owned code is licensed under the **European Union Public Licence 1.2 (EUPL-1.2)**. Third-party dependencies and assets retain their own licences and notices; see [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).

---

**High-assurance inside. KISS outside. Slightly unhinged by design.** 🦌
