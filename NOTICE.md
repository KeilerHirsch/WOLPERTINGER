# Notices, provenance, and project boundaries

WOLPERTINGER is an independent, community-built companion project for Elite Dangerous.

It is not affiliated with, endorsed by, sponsored by, or approved by Frontier Developments.

Elite Dangerous, Frontier Developments, and related names, trademarks, artwork, game assets, and other protected material remain the property of their respective rights holders.

## Clean-room boundary

WOLPERTINGER is intended to be independently designed and implemented from public or documented interfaces, lawful user-observable behaviour, and data that may be used under its applicable terms.

The project does **not** incorporate EDCoPilot source code, decompiled code, proprietary assets, copied interface assets, or other non-public implementation material.

References to existing community tools are for interoperability research, comparison, and attribution where appropriate — not a claim of ownership or endorsement.

## Code, data, and assets are separate

Copyright © 2026 KeilerHirsch. WOLPERTINGER core source code is licensed under **EUPL v. 1.2 only** unless a file or component states otherwise.

That licence does not automatically relicense third-party data, APIs, documentation, game assets, trademarks, community datasets, or dependencies. Each external source retains its own provenance, licence, terms, and rights.

Runtime dependencies used by the current foundation slice are listed below. Future external data sources and assets remain subject to their own provenance and licence review.

## Third-party implementation dependencies

- `cbor_ada` 0.3.0, pinned to commit `b448c366117ff9f6c050b13d4fe609bb79495759`, is used as a third-party CBOR dependency and remains licensed under Apache-2.0. Its source and licence are not relicensed as EUPL-1.2.

- System.Formats.Cbor 10.0.11 is used by the .NET contract implementation and remains licensed under MIT.
- Microsoft.Data.Sqlite 10.0.11 is used only for rebuildable read-model projections and remains licensed under MIT.
