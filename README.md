# WOLPERTINGER

**Wide-Area Operations & Logistics Platform for Exploration, Routing, Telemetry, Intelligence, Navigation, Guidance, Engineering, and Reconnaissance**

WOLPERTINGER is an open-source, local-first companion platform for **Elite Dangerous**. Its goal is simple: keep navigation, exploration, engineering context, telemetry, and shipboard assistance coherent so commanders spend less time operating menus and more time flying.

> **Project status: Foundation / pre-alpha**
> The architecture is frozen and the first retained `FSDJump` vertical slice is now being implemented. There is **no supported end-user release yet**.

<p align="center">
  <img src="docs/assets/wolpertinger-concept-ui.webp" alt="WOLPERTINGER concept UI showing route, exploration, engineering, and shipboard-assistant context in an Elite Dangerous cockpit" width="100%">
</p>

<p align="center"><sub><strong>Concept UI — early development.</strong> This image communicates product direction and intended workflow; it is not a screenshot of implemented feature status.</sub></p>

## What WOLPERTINGER is for

The intended product experience is one context-aware shipboard assistant rather than a pile of disconnected panels:

- **Navigate smarter** — surface route and jump context when it matters.
- **Explore with less menu archaeology** — keep relevant exploration signals and observations close to the current situation.
- **Engineer with context** — connect material and engineering information to what the commander is actually doing.
- **Stay informed without chatter** — deterministic facts first; optional voice/personality layers may explain them later, but never invent authoritative game state.

Those broader product areas are direction, not a claim that they are all implemented today. The first implementation target is deliberately smaller: one complete, deterministic `FSDJump` path from journal evidence through trusted state and replay.

## Quick start

WOLPERTINGER is currently for contributors and architecture review, not end-user installation.

```powershell
git clone https://github.com/KeilerHirsch/WOLPERTINGER.git
cd WOLPERTINGER
dotnet --version
```

The pinned .NET SDK is **10.0.111**. Ada/SPARK toolchain setup and the exact implementation sequence are tracked in the [FSDJump vertical-slice plan](docs/superpowers/plans/2026-09-06-fsdjump-vertical-slice.md).

If you only want to understand the design first, start with the [foundation design](docs/superpowers/specs/2026-09-06-wolpertinger-foundation-design.md) and [roadmap](ROADMAP.md).

## Architecture in 30 seconds

```text
Elite Dangerous journal / future sources
        |
        v
.NET 10 Edge/Core Host
  - volatile I/O + JSON
  - append-only raw evidence
  - deterministic normalization + normalized ledger
        |
        v
bounded, versioned CBOR/CDDL contract
        |
        v
Ada/SPARK Trusted Kernel x2
  - Active + hot-passive Shadow
  - canonical authoritative state
  - deterministic facts + state digest
        |
        v
.NET context/output layer
  - deterministic relevance/output
  - later UI / voice / plugins remain non-authoritative
```

The headless authoritative path is intentionally isolated from UI, network integrations, plugins, TTS/STT, and LLMs. Raw evidence and normalized observations are durable and replayable; SQLite is only a rebuildable projection store.

**Foundation rule:** two kernels compute, one authority decides, one writer publishes. Identity ambiguity, sequence gaps, integrity conflicts, and unrepresentable trusted numerics fail closed instead of being guessed around.

## Foundation decisions

- **Edge/Core Host:** C# / .NET 10
- **Trusted Kernel:** Ada 2022 / SPARK, isolated process boundary
- **Kernel topology:** Active + hot-passive Shadow with supervisor epoch/fencing
- **Historical truth:** append-only raw evidence + append-only normalized ledger
- **Trusted contract:** bounded deterministic CBOR with CDDL schema
- **Replay:** same ordered observations must reproduce the same authoritative state digest
- **Identity/session semantics:** explicit and fail-closed
- **AI boundary:** optional future explanation/personality only; never authoritative state
- **WOLPERTINGER-owned code licence:** EUPL-1.2 only

The full rationale, invariants, failure semantics, and scope guard live in the [foundation design](docs/superpowers/specs/2026-09-06-wolpertinger-foundation-design.md).

## Support the project

If WOLPERTINGER becomes useful to you, GitHub's **Sponsor this project** surface links to the project's configured funding target. Funding is voluntary and gives **no feature, ranking, review, release, roadmap, or support privileges**.

## Licence

WOLPERTINGER-owned code is licensed under the **European Union Public Licence 1.2 (EUPL-1.2)**. Third-party dependencies and assets retain their own licences and notices; see [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).

---

**Simple by default. Powerful by choice. Slightly unhinged by design.** 🦌
