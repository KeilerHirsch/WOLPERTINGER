# WOLPERTINGER

**Wide-Area Operations & Logistics Platform for Exploration, Routing, Telemetry, Intelligence, Navigation, Guidance, Engineering, and Reconnaissance**

*Your copilot should know what to do.*

**High-assurance inside. KISS outside.**

WOLPERTINGER is an open-source, local-first companion platform for **Elite Dangerous**. One authoritative core turns game context into useful facts and presents them through the surface that fits the moment.

> **Status: Pre-alpha.** The trusted Stage-1 foundation is complete; presentation work is in progress. There is **no supported end-user release yet**.

<p align="center">
  <img src="docs/assets/wolpertinger-concept-ui.webp" alt="WOLPERTINGER concept UI showing route, exploration, engineering, and shipboard-assistant context in an Elite Dangerous cockpit" width="100%">
</p>

<p align="center"><sub><strong>Concept UI — early development.</strong> Product direction, not an implemented-feature screenshot.</sub></p>

## In 30 seconds

WOLPERTINGER is designed around three rules:

- **Useful before configured.** Local Journal/Status data should provide value without provider accounts or API keys.
- **Context instead of panel archaeology.** Relevant information should come forward with the commander's current task.
- **One core, multiple surfaces.** Tray, overlays, hub, diagnostics and later passive voice consume the same authoritative facts.

The commander should not be the integration bus. Optional providers and future assistant layers may add capability, but they must not become the owner of local game truth.

## What exists today

Implemented on `main`:

- durable raw evidence and a normalized observation ledger;
- deterministic replay and rebuildable projections;
- bounded CBOR/CDDL trusted contract;
- Ada/SPARK Active + hot-passive Shadow kernels;
- authority epoch/fencing and canonical state digest;
- retained `FSDJump → JumpFact → deterministic output` vertical slice;
- typed presentation contracts and validation;
- trusted fact projection into presentation snapshots;
- bounded latest-state publishing and local named-pipe transport;
- presentation state/ViewModels plus Windows-facing overlay/hub infrastructure.

Still in active development: the commander-facing tray/overlay/hub experience, reconnect/recovery UX, passive voice/TTS, optional providers and broader Elite domain workflows.

## Architecture

```text
Elite Journal / Status / future optional sources
        |
        v
.NET 10 Edge Host
  durable evidence -> deterministic normalization -> normalized ledger
        |
        v
bounded, versioned CBOR/CDDL contract
        |
        v
Ada/SPARK Trusted Kernel x2
  Active + hot-passive Shadow
  authoritative state + deterministic facts
        |
        v
.NET context / presentation projection
        |
        +--> Tray / Overlay / Docks
        +--> Fullscreen Hub
        +--> Diagnostics
        +--> later passive Voice/TTS
```

The trusted path is intentionally isolated from UI, external services, plugins, TTS/STT and LLMs. Presentation consumes trusted facts; it does not manufacture them.

## Verify it locally

WOLPERTINGER is currently aimed at contributors and architecture review.

Requirements:

- .NET SDK **10.0.111**
- Alire **2.1.1**
- GNAT **16.1.0**
- GPRbuild **26.0.1**

Build and test the .NET side:

```powershell
dotnet restore WOLPERTINGER.slnx
dotnet build WOLPERTINGER.slnx -c Release --no-restore
dotnet test WOLPERTINGER.slnx -c Release --no-build
```

Build and test the trusted kernel:

```powershell
Push-Location kernel
alr build --validation
Pop-Location

Push-Location kernel/tests
alr build --validation
alr run
Pop-Location
```

Run the SPARK proof boundary:

```powershell
Push-Location kernel/proof
alr exec -- gnatprove -P wolpertinger_kernel_proof.gpr --level=2 --report=all
Pop-Location
```

The same gate runs in GitHub Actions on pushes and pull requests.

## Engineering rules

- **Evidence before claims.** Replay, tests and proof artifacts back important statements.
- **Deterministic where it matters.** Same ordered evidence should reproduce the same authoritative result.
- **Fail visibly.** Optional failures degrade; uncertainty and stale data remain labelled.
- **KISS outside. Assurance inside.** Complexity belongs behind the user-facing surface.

AI-assisted tools may be used during research, review, documentation and implementation. They are development aids, not runtime authorities.

## Roadmap

See [ROADMAP.md](ROADMAP.md) for the current high-level sequence. Directions are not release promises.

## Support

Funding is voluntary and gives no feature, ranking, review, release, roadmap, support or technical-influence privileges.

## Licence

WOLPERTINGER-owned code is licensed under the **European Union Public Licence 1.2 (EUPL-1.2)**. Third-party dependencies and assets retain their own licences and notices; see [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).

---

**High-assurance inside. KISS outside. Slightly unhinged by design.** 🦌
