# WOLPERTINGER

**Wide-Area Operations & Logistics Platform for Exploration, Routing, Telemetry, Intelligence, Navigation, Guidance, Engineering, and Reconnaissance**

## Your copilot should know what to do.

WOLPERTINGER is an open-source companion platform for **Elite Dangerous**, built around a simple idea:

**A copilot should reduce your workload — not become another workload.**

It is designed to follow the commander, understand the current game state, surface what actually matters, and stay quiet when nothing does.

Exploration, navigation, logistics, engineering, colonisation, telemetry, and voice assistance are treated as parts of one coherent system rather than a collection of disconnected panels.

Under the hood, WOLPERTINGER is local-first, event-driven, and deterministic where facts matter. Game state comes from explicit data sources and traceable calculations. AI may provide conversation, personality, and explanations — but it does not get to invent reality.

**The goal is simple: spend less time operating the companion and more time flying the ship.**

> **Project status: Foundation / pre-alpha**
>
> WOLPERTINGER is being designed in public. Architecture, interfaces, provenance, and contribution boundaries come before feature volume.

## Foundation decisions

- **Language/runtime:** C# on .NET 10 LTS
- **Core licence:** EUPL-1.2
- **Architecture:** local-first, event-driven, modular, and replayable
- **State:** one canonical game-state model with explicit provenance and freshness
- **AI boundary:** optional personality and explanation layer; never authoritative game state
- **Extensions:** plugin-ready contracts are part of the foundation, not an afterthought
- **Development model:** clean-room implementation from documented/public interfaces and properly licensed data sources

## Design principles

- Useful defaults before configuration.
- Progressive disclosure instead of a wall of settings.
- Context before chatter: speak when something matters.
- Facts stay facts; personality stays personality.
- A stale or unknown value is labelled as such instead of guessed.
- Diagnostics and replay should make bugs reproducible.
- External data, code, assets, and trademarks keep their own provenance and rights.

**Advanced configuration will exist. Most commanders should never need it.**

*Those who insist may enter the nerd basement at their own risk.* 🦌
