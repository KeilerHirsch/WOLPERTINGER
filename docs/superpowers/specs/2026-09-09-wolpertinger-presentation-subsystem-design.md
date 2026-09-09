# WOLPERTINGER Presentation Subsystem Design

**Status:** Proposed architecture freeze for the presentation subsystem

**Date:** 2026-09-09

**Prerequisite:** Stage 1 / Trusted Foundation is closed on `main` at merge commit `bc74adc7362af3993d0ead1a1e624d7d6d7535e4`.

**Normative foundation:** `docs/superpowers/specs/2026-09-06-wolpertinger-foundation-design.md`

**Presentation principle:** **ONE CORE — MULTIPLE PRESENTATION SURFACES.**

## 1. Scope

This document freezes the presentation architecture required to begin a first retained user-interface vertical slice without reopening general WOLPERTINGER FuE.

The subsystem consumes already-established trusted facts and deterministic decisions. It does not become another game-state engine, recommendation engine, persistence authority, or source of provenance.

Release 1 is **Windows-first, cross-platform-ready**. Windows receives the complete first release experience. Linux / SteamOS remains architecturally possible through a later platform adapter. macOS is not a Release-1 target.

The first retained presentation slice is deliberately narrow:

```text
real Stage-1 JumpFact
  -> Presentation Contract
  -> surface-neutral Presentation State / JumpViewModel
  -> Tray control plane
  -> one simple Windows docked overlay
  -> the same commander content in a Fullscreen Hub
```

## 2. Goals

- Reuse one authoritative presentation state across tray, overlay, Fullscreen Hub, and diagnostics.
- Preserve the Stage-1 trust boundary: presentation consumes facts; it never recreates truth.
- Keep surface-neutral ViewModels free of HWNDs, Win32 APIs, physical-monitor handles, and OS window semantics.
- Isolate Windows-only windowing behavior behind one platform surface adapter.
- Make presentation restartable without requiring trusted-state reconstruction or core restart.
- Give commanders concise, action-oriented views while retaining audit-grade provenance and diagnostics separately.
- Recover safely from monitor removal, DPI changes, display-topology changes, and presentation-process failure.
- Keep normal configuration bounded: good defaults first, presets second, advanced controls in the nerd basement.

## 3. Explicit non-goals

This design does not include:

- a full recreation of the concept UI;
- voice, STT, TTS, or conversational AI;
- CAPI, EDDN, community-network ingestion, or broader journal coverage;
- engineering, exploration, trade, mining, colonisation, BGS, or Powerplay feature completion;
- plugin-system expansion;
- a Linux / Wayland / X11 implementation;
- macOS implementation;
- a blank-canvas dashboard builder;
- arbitrary user-defined widgets or scripts;
- a second recommendation or gameplay-state engine;
- final pixel-level visual design;
- a frozen UI toolkit choice.

Concrete UI framework selection is intentionally an implementation-stage choice. It must satisfy this specification without changing the neutral contracts or trusted boundary.

## 4. Frozen subsystem topology

```text
Trusted Kernel / .NET Edge
        |
        v
Presentation Contract
        |
        v
Surface-neutral Presentation State + ViewModels
        |
        +--> Tray Surface
        +--> Overlay Surface
        +--> Fullscreen Hub
        +--> Diagnostics Surface
                 |
                 v
        Platform Surface Adapter
                 |
                 +--> Windows Adapter (Release 1)
```

The user-facing presentation shell is a separate .NET process from the authoritative Edge/Trusted Kernel path. A presentation crash may remove windows, but it must not terminate, corrupt, reinterpret, or gain authority over trusted state.

The presentation shell may be restarted and rehydrated from the current presentation snapshot. It does not require replay of the historical evidence log merely to redraw the UI.

The physical local transport between Edge and the presentation shell is not part of the trusted-kernel protocol. Its implementation may be selected during planning behind a neutral connection interface. The presentation contract semantics remain independent of named pipes, Unix-domain sockets, loopback transport, or any later transport choice.

The existing bounded CBOR/CDDL Trusted Kernel contract must not be expanded merely to satisfy UI convenience.

## 5. Ownership boundaries

### 5.1 Trusted Kernel / Edge owns truth

The existing authoritative path continues to own:

- canonical game-state transitions;
- trusted facts such as `JumpFact`;
- deterministic context/relevance decisions;
- evidence references and digests;
- authoritative state digests;
- provenance and freshness semantics;
- failure and identity semantics.

Presentation may display those values. It may not recompute, replace, weaken, or silently reinterpret them.

### 5.2 Presentation Contract owns read-side transfer semantics

The Presentation Contract is an immutable read-side contract between Edge and the presentation shell. It exposes only trusted facts, deterministic decisions, presentation health, and metadata needed to render them correctly.

It may add presentation delivery metadata, such as a monotonic presentation revision, but such metadata is never authoritative gameplay order. Gameplay identity and ordering continue to come from authoritative cursors, profile/session identity, evidence references, and state digests.

The contract must support two operations conceptually:

1. obtain a complete current presentation snapshot; and
2. subscribe to subsequent presentation updates.

A reconnecting presentation process requests a fresh snapshot rather than treating missed UI updates as lost historical truth.

### 5.3 Surface-neutral Presentation State and ViewModels

Surface-neutral state maps the Presentation Contract into semantic display state. It may perform deterministic presentation-only transformations such as unit formatting, label selection, grouping, density selection, and visibility rules that do not change the underlying fact.

Neutral ViewModels must not reference:

- HWND or other native window handles;
- Win32/PInvoke APIs;
- monitor device names or OS display handles;
- DPI APIs;
- window z-order flags;
- tray APIs;
- platform-specific screen geometry.

The same neutral commander ViewModel must be reusable by overlay and Fullscreen Hub. A surface may choose a different layout or density, but it must not maintain a divergent copy of gameplay truth.

### 5.4 Surface layer owns composition

A surface owns layout, visual hierarchy, local interaction affordances, visibility, density, and surface-specific navigation.

It does not own domain rules. If a future interaction can affect WOLPERTINGER state, the surface emits an explicit user intent to Edge through a command boundary; it never mutates canonical state directly.

The first JumpFact slice contains no gameplay-affecting UI command path. Surface actions are limited to presentation lifecycle and presentation preferences.

### 5.5 Platform Surface Adapter owns OS behavior

Only the platform adapter may own:

- tray integration;
- native window creation semantics;
- focusless/no-activate behavior;
- click-through behavior;
- always-on-top behavior;
- display enumeration and topology change events;
- monitor selection and safe placement;
- DPI awareness and DPI-change handling;
- native window recovery when a display disappears.

The Release-1 implementation provides a Windows adapter. A later Linux adapter must be able to implement the same neutral surface requests without changing Trusted Core, Presentation Contract, or surface-neutral ViewModels.

The Windows adapter may use Win32/PInvoke internally where the selected UI toolkit does not expose required behavior. Those calls remain isolated to the Windows adapter assembly and are covered by adapter tests.

## 6. Presentation Contract

The contract is typed, versioned, immutable, and read-oriented. It is not a general remote-control API.

For the first slice, the contract must carry enough information to reconstruct a `JumpPresentationState` without reaching back into raw journal JSON or kernel internals.

At minimum, a jump presentation update carries the authoritative observation cursor, profile identity, evidence reference/digest, state digest, system address/name, galactic position, jump distance, fuel used, remaining fuel, provenance/freshness for location and fuel, and the deterministic reason code from the Stage-1 context decision.

Contract invariants:

- Unknown contract versions are rejected explicitly; they are not guessed around.
- Missing mandatory authoritative identifiers make an update unusable.
- Presentation never converts `Unknown`, `Stale`, or `Conflicting` into a fabricated normal value.
- Presentation delivery revision may order redraws but never supersedes the authoritative observation cursor.
- A full snapshot is self-contained for rendering the current UI state.
- Updates are idempotent at the presentation layer when their authoritative identity and content are unchanged.
- Presentation contract serialization is independently versioned from the SPARK trusted contract.

No presentation consumer may parse Frontier journal JSON to fill missing contract fields.

## 7. State and data flow

The retained flow is:

```text
Journal / Stage-1 source
  -> existing durable evidence + normalization
  -> SPARK Active + Shadow
  -> trusted result
  -> JumpFact
  -> deterministic ContextDecision
  -> Presentation Contract projection
  -> current Presentation Snapshot / update stream
  -> surface-neutral JumpViewModel
  -> Tray / Overlay / Fullscreen Hub
```

The contract projection is downstream of authoritative processing. A failure to render or deliver presentation data must not roll back or alter the trusted transition.

Presentation state is rebuildable. Gameplay truth is not durably owned by the presentation process. Only bounded UI preferences such as selected density, preferred dock edge, visibility, and preferred display target may be persisted by presentation.

## 8. Surface matrix and responsibilities

Frozen presentation surfaces and density modes are:

- Tray
- Left Dock
- Right Dock
- Top Strip
- Bottom Strip
- Compact
- Standard
- Expanded
- Fullscreen Hub
- Diagnostics / SYS
- Voice later as an additional I/O surface

### 8.1 Tray — lifecycle and control plane

The tray is the normal entry point and lifecycle owner for the presentation shell.

It owns:

- showing, hiding, and restoring presentation surfaces;
- switching bounded overlay presets/density;
- opening the Fullscreen Hub;
- opening diagnostics;
- reporting presentation/core connectivity at a glance;
- presentation-shell exit;
- access to advanced settings without making them the normal workflow.

Tray state is not gameplay state. Closing a commander window does not stop or rewrite the trusted core. Any future command that intentionally stops core services must be explicit and separate from merely exiting or hiding the UI shell.

### 8.2 Overlay — glanceable commander surface

The overlay is a docked commander view, not a movable desktop application window by default.

Its primary job is to show a small amount of current, trusted context without stealing attention from Elite Dangerous.

Release-1 placement presets are Left Dock, Right Dock, Top Strip, and Bottom Strip. Density presets are Compact, Standard, and Expanded.

The first slice implements one simple dock placement and enough adapter structure to prove the other placements do not require a different state model.

Overlay rendering must remain valid when hidden and shown repeatedly. Hiding an overlay never pauses authoritative ingestion.

### 8.3 Fullscreen Hub — deliberate deep view

The Fullscreen Hub is an explicit, user-opened surface intended for deliberate inspection on the game display or a secondary monitor.

It consumes the same Presentation State as the overlay. It may show more fields, explanation, provenance, history, or navigation because it has more space, but it does not query a second gameplay-state source.

The first slice displays the same JumpFact commander content in a larger composition. It does not attempt to implement the complete concept UI.

### 8.4 Diagnostics / SYS — engineering view

Diagnostics is a separate engineering surface for operational evidence, not a commander dashboard with extra toggles.

It may expose contract version, authoritative cursor, evidence reference/digest, state digest, provenance/freshness, connection state, display topology, adapter state, and presentation faults.

Diagnostics remains read-only with respect to canonical game state. Debug actions must not bypass trusted-core authority or fencing.

## 9. Windows overlay semantics

### 9.1 Focusless / no-activate

A passive overlay must not activate itself, steal keyboard focus, move the foreground window away from Elite Dangerous, or become the task the commander must manage merely because new data arrived.

Showing, updating, resizing, or repositioning the passive overlay uses no-activate semantics through the Windows adapter.

If a later interactive overlay mode is added, entry into that mode must be an explicit user action and its focus behavior must be visibly different from passive mode. Interactive mode is not required by the first slice.

### 9.2 Click-through

The passive overlay is click-through by default. Pointer input intended for the game must not be intercepted by transparent or visible overlay regions.

Click-through is a windowing property owned by the Windows adapter, not by the JumpViewModel.

### 9.3 Always-on-top

The passive overlay requests topmost behavior suitable for normal windowed/borderless game use. This requirement does not justify process injection, game hooks, graphics API interception, or anti-cheat-sensitive techniques.

If the operating environment prevents a safe overlay from being visible, WOLPERTINGER degrades to Tray and Fullscreen Hub rather than introducing invasive rendering techniques.

### 9.4 Surface bounds

Dock placement is computed from the resolved target display's usable bounds. Neutral code requests a dock edge and density; the Windows adapter resolves that request to native coordinates.

A docked overlay must remain fully recoverable through the tray even if a placement operation fails.

## 10. Multi-monitor and DPI behavior

Display topology is runtime state owned by the platform adapter. It is not stored inside gameplay ViewModels.

The adapter resolves neutral placement preferences against currently available displays and exposes only resolved presentation capabilities upward.

Requirements:

- Per-monitor DPI changes trigger bounds recalculation before the surface is considered settled.
- Stored size/placement preferences are interpreted through current DPI, not reused as raw physical pixels across displays.
- Surfaces are clamped to visible usable bounds after placement.
- Fullscreen Hub targets one resolved display at a time and recomputes when the target topology changes.
- No neutral assembly may persist or compare native monitor handles as stable product identity.

### 10.1 Display-topology recovery

If the display hosting an overlay or Fullscreen Hub disappears, no WOLPERTINGER window may remain stranded off-screen.

Recovery order is:

1. use the currently known game display if it remains available;
2. otherwise use the current primary display;
3. clamp the surface to the selected display's usable bounds;
4. keep the tray available as the final recovery/control surface.

The adapter may retain the user's preferred target for future launches, but it must not automatically teleport an already recovered visible window back when a disconnected monitor merely reappears during the same session.

Topology recovery changes window placement only. It never changes gameplay state or presentation fact content.

## 11. Presentation lifecycle

Presentation startup follows this sequence:

```text
start presentation shell
  -> initialize platform adapter + tray
  -> connect to Edge presentation endpoint
  -> request current snapshot
  -> validate contract version and mandatory identifiers
  -> build surface-neutral ViewModels
  -> show configured surfaces
  -> subscribe to subsequent updates
```

Presentation shutdown hides/disposes surfaces and closes its presentation connection. It does not require an authoritative state transition.

Presentation restart follows the same startup path and rehydrates from a fresh snapshot. It must not depend on stale in-memory ViewModels from the previous process.

A temporary connection loss preserves the last valid snapshot only as historical display context and marks it unavailable/stale at the presentation level. It must never silently present the frozen data as live.

When connectivity returns, a fresh valid snapshot atomically replaces the disconnected view before normal updates resume.

UI preference persistence is separate from gameplay-state persistence. Corrupt or missing UI preferences fall back to safe defaults rather than blocking the trusted core.

## 12. Failure containment

Presentation failure must degrade presentation, not truth.

Failure rules:

- Presentation process crash: Edge and both trusted kernels continue independently.
- Surface render exception: isolate the failing surface where possible; retain tray recovery and log a presentation diagnostic.
- Platform adapter failure: affected native behavior fails closed to a safe ordinary window or hidden surface; it never fabricates game data.
- Unsupported Presentation Contract version: reject connection/update and show an explicit compatibility fault.
- Invalid mandatory presentation payload: reject that payload, retain the last valid snapshot as non-live context, and diagnose the rejection.
- Core disconnect: visibly mark presentation unavailable; do not invent defaults, zero fuel, empty system names, or synthetic freshness.
- Display removal: recover placement using the topology rules in section 10.
- Corrupt UI preferences: discard only the invalid preferences and use safe defaults.
- Diagnostics failure: commander surfaces continue if their own validated snapshot remains usable.

A presentation exception must never be allowed to call into Trusted Kernel mutation APIs as a recovery mechanism.

## 13. Provenance and freshness UI grammar

The UI must preserve the semantic distinction between:

- known and current;
- stale;
- unknown;
- conflicting;
- unavailable because the presentation connection is lost.

These states must not be communicated by color alone.

Fresh, unambiguous local trusted data may remain visually quiet in Compact commander views. Stale, conflicting, or unavailable state that can change commander action must be explicit.

Expanded and Fullscreen Hub views must provide a discoverable provenance/freshness detail for displayed facts. Diagnostics may show the exact source classification, cursor, evidence reference, evidence digest, and state digest.

Presentation must not invent confidence percentages or collapse multiple provenance classes into a misleading generic "verified" badge.

## 14. Commander View != Engineering View

Commander surfaces optimize for the next useful action, not for proving the architecture to the user.

Commander views should prefer:

- current system/context;
- values that affect the next action;
- concise status and exceptions;
- progressive disclosure instead of permanent telemetry walls.

Engineering/Diagnostics views may expose:

- evidence and state digests;
- authoritative cursors and contract versions;
- provenance/freshness detail;
- kernel/presentation connectivity;
- display-adapter and topology state;
- presentation faults and recovery decisions.

Both view classes consume the same underlying Presentation State. Engineering detail is an alternate projection of the same facts, not a privileged second database.

Normal commander operation must not require opening Diagnostics or understanding epochs, evidence ordinals, CBOR, or monitor handles.

## 15. Configuration model

The configuration hierarchy is frozen as:

**good defaults > presets > bounded options > nerd basement**

The normal user chooses among a small set of sensible placement/density presets. Advanced options exist only where a real commander need or recovery case justifies them.

The product must not require users to construct the UI from an empty canvas, choose among dozens of equivalent widgets, or understand platform window flags.

The first slice persists only the smallest useful preference set: surface visibility, dock preset, density preset, and preferred display target.

## 16. First JumpFact presentation slice

The first retained slice consumes the real Stage-1 `JumpFact` produced after successful trusted-kernel processing.

The initial `JumpPresentationState` / `JumpViewModel` includes at least:

- authoritative observation cursor;
- profile identity required for correct commander context;
- system address and star-system name;
- galactic position;
- jump distance;
- fuel used and remaining fuel;
- location provenance and freshness;
- fuel provenance and freshness;
- evidence reference/digest;
- authoritative state digest;
- deterministic context reason code;
- presentation connection/availability state.

No field in this ViewModel is reconstructed by reparsing journal JSON.

The Overlay and Fullscreen Hub bind to the same commander ViewModel instance or equivalent immutable value projection from the same current Presentation State. Their field values must therefore agree for the same presentation revision.

First-slice surface behavior:

- Tray starts before commander windows and remains the recovery/control entry point.
- The overlay starts in passive, focusless, click-through, topmost mode.
- One dock preset is implemented end-to-end; the placement abstraction must already represent all four frozen dock positions.
- Compact/Standard/Expanded are represented as bounded density semantics even if the first retained visual proves only the default density.
- Fullscreen Hub renders the same jump state in a larger layout.
- Diagnostics may expose the first slice's cursor/digest/provenance data, but a complete diagnostics product is outside this slice.
- Killing and restarting the presentation shell rehydrates the latest available trusted presentation snapshot.

The first slice does not change Stage-1 acceptance semantics or reopen kernel hardening.

## 17. Testing strategy

Presentation tests are split by boundary so Windows GUI behavior does not contaminate trusted-core tests.

### 17.1 Contract projection tests

Given known Stage-1 `JumpFact` and `ContextDecision` fixtures, projection tests verify exact transfer of authoritative identifiers, values, provenance, freshness, evidence metadata, state digest, and reason code.

Tests must prove that presentation projection does not parse raw journal JSON and does not silently substitute missing mandatory fields.

### 17.2 Surface-neutral ViewModel tests

Pure .NET tests verify deterministic formatting, density rules, visibility rules, unavailable/stale grammar, and equality of commander data exposed to Overlay and Fullscreen Hub.

These tests run without native windows, Win32, physical monitors, or an interactive desktop.

### 17.3 Tray and lifecycle tests

Tests verify that tray commands show/hide the expected surfaces, that presentation exit does not imply trusted-core shutdown, and that restart requests a fresh snapshot rather than relying on previous-process state.

A process-level integration test must terminate the presentation shell while the Edge/Trusted Kernel path remains alive, then restart presentation and verify rehydration from the latest snapshot.

### 17.4 Windows adapter tests

The Windows adapter is tested behind injectable abstractions for window and display operations.

Automated tests cover:

- passive no-activate intent;
- click-through intent;
- topmost intent;
- dock geometry for all four frozen positions;
- bounds clamping;
- DPI transitions at representative scale factors;
- target-display disappearance and safe fallback;
- Fullscreen Hub recovery after topology change;
- failure of one native surface without gameplay-state mutation.

A Windows smoke test may additionally exercise actual native window styles and display events. Such smoke coverage supplements, but does not replace, deterministic adapter-policy tests.

### 17.5 Architecture boundary tests

Automated dependency tests or equivalent build-time checks must fail if neutral presentation assemblies reference Win32/PInvoke-specific assemblies or platform-adapter types.

The presentation project must not reference raw journal parsers for normal state construction.

## 18. First-slice acceptance criteria

The first presentation vertical slice is accepted only when all of the following are true:

1. A real Stage-1 `FSDJump` path produces a presentation update only after the trusted transition succeeds.
2. The presentation update carries the authoritative cursor, evidence identity/digests, state digest, provenance/freshness, and deterministic reason code required by this design.
3. Overlay and Fullscreen Hub render the same commander values from one current Presentation State for the same revision.
4. Tray can show/hide the first overlay and open/close the Fullscreen Hub without mutating gameplay state.
5. The passive Windows overlay is focusless/no-activate, click-through, and topmost through the Windows adapter boundary.
6. No Win32 or physical-monitor dependency exists in Presentation Contract or surface-neutral ViewModel assemblies.
7. Removing the target display causes safe recovery to an available display; no surface remains stranded off-screen.
8. A DPI/topology change recalculates and clamps native bounds without changing fact content.
9. Killing the presentation process does not kill or corrupt Edge/Trusted Kernel processing.
10. Restarting presentation rehydrates from a fresh valid snapshot and resumes updates without replaying raw evidence solely for UI recovery.
11. A presentation disconnect or invalid payload is visibly non-live and never replaced with fabricated defaults.
12. Stage-1 Release build, Edge tests, and Integration tests remain green after the presentation slice is added.
13. The first slice does not require voice, CAPI, EDDN, plugins, Linux, or broad feature-module work to pass.

## 19. Release-1 platform boundary

Windows is the only platform required to satisfy the complete Release-1 surface behavior in this specification.

Linux / SteamOS later supplies another Platform Surface Adapter. Porting must not require moving Win32 concepts into neutral contracts or rewriting trusted facts.

macOS remains outside Release-1 scope.

## 20. Explicit implementation-stage choices

The following choices are intentionally not frozen by this architecture spec and may be selected during implementation planning without reopening general FuE, provided they satisfy every boundary and acceptance criterion above:

- concrete cross-platform-capable .NET UI toolkit;
- physical local Presentation Contract transport;
- exact presentation serialization format;
- exact Windows API calls needed for native surface behavior;
- visual styling, typography, spacing, and animation;
- exact default dimensions for each density/dock preset;
- internal preference-file format.

If one of these choices cannot satisfy the frozen architecture, research is limited to that concrete blocker and stops once the blocker is resolved.

## 21. Scope guard

WOLPERTINGER is not a user-programmable dashboard framework.

New surface abstractions, widget registries, rendering engines, layout DSLs, plugin hooks, or cross-platform compatibility layers require a concrete first-slice need before they are added.

The first implementation should prefer a few small projects/assemblies with explicit dependencies over a generic shared UI framework invented for hypothetical future products.

Do not build a BRUNHILDE/WOLPERTINGER shared presentation framework now. WOLPERTINGER proves the pattern first. Reusable engineering patterns may be harvested later only after they are validated here.

## 22. BRUNHILDE engineering note

This subsystem is a practical proving ground for later reuse of validated ideas: presentation contracts, process failure containment, tray supervision, surface-neutral ViewModels, multi-monitor recovery, provenance/freshness grammar, and Windows adapter isolation.

That is permission to reuse proven patterns where appropriate, not permission to couple repositories, merge product requirements, or create speculative cross-project abstractions.

## 23. Freeze statement

Upon approval of this written specification, the WOLPERTINGER presentation subsystem is frozen at architecture level for the first retained presentation vertical slice.

The implementation plan may choose concrete libraries and native mechanisms, but it must preserve:

- one authoritative core;
- one Presentation Contract;
- surface-neutral ViewModels;
- multiple views over the same Presentation State;
- Windows-only OS behavior behind the Windows adapter;
- restartable presentation-process isolation;
- explicit provenance/freshness semantics;
- safe display-topology recovery;
- KISS configuration boundaries.

No implementation begins before the written-spec review gate and the subsequent TDD implementation plan are complete.

The working rule remains:

> **Simple by default. Powerful by choice. Slightly unhinged by design.**
