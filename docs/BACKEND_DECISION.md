# Backend decision record (ADR-001)

**Status:** accepted for prototype → pre-production. **Date:** 2026-11-19.

## Requirements that actually decide this

| Requirement | Consequence |
| --- | --- |
| Server-authoritative combat, XP, inventory, currency | We need to run **our own simulation loop** on demand; per-action server logic must be cheap and testable |
| 10–20 player world-boss raids, 3v3/5v5 arenas, real-time movement | Sub-100 ms tick authority; predictable CPU cost per player |
| Cross-platform accounts (PC/Android/iOS) | Provider-neutral auth token + our own profile store |
| Android + iOS IAP with server verification | Our backend must hold purchase receipts and the currency ledger |
| Cost control at low CCU (a launch MMO starts small) | Prefer usage pricing that starts at zero; avoid per-MAU spikes |
| Solo team, 12 phases | Managed services beat self-hosting; no k8s fleet until load tests justify it |

## Options compared

| Option | Authority | Cost at 0 → 500 CCU | Verdict |
| --- | --- | --- | --- |
| **Unity Gaming Services** (Auth, Cloud Save, Relay, Lobby, Netcode for GameObjects) | Relay is *not* authoritative; you still host a server process for combat | Free tier generous, then per-CCU/GB | Good for lobby/auth; insufficient alone for authority |
| **Unity Dedicated Server / Multiplay** | Yes, full authority | Free tier small; per-server-hour after | Real option at scale, overkill at prototype |
| **PlayFab** (Economy, Inventory, Matchmaking, Party, CloudScript) | CloudScript/Azure Functions, not a game loop | Free 1k profiles, then per-profile | Strong for economy/IAP; weak for 20 Hz combat |
| **Photon Fusion / Quantum** | Yes (Quantum deterministic) | Per-CCU monthly | Excellent netcode, but locks authority into their cloud and bills per CCU from day one |
| **Custom Node/Go server + Postgres/Redis** (this repo) | Yes, full control | ~$15/mo single node | Chosen for prototype & pre-production |

## Decision

1. **Own authoritative server** (this repository, TypeScript on Node 24) for the world simulation and
   all integrity-critical rules. It is the same ruleset as the client, re-derived server-side — never
   trusted from the client.
2. **Unity Gaming Services Authentication is acceptable for identity** later, but the prototype uses
   our own HMAC-token auth so tokens are provider-neutral and testable outside Unity (they are, see
   `server/test`).
3. **PlayFab / UGS Economy are not used**; the ledger and inventory are ours so IAP verification and
   refunds are auditable end-to-end (requirement 17H).
4. **Transport:** WebSocket + JSON for the prototype (debuggable, works everywhere). Production keeps
   the same message contract over a binary codec (MessagePack/KCP) behind `NetClient`'s interface.
5. **Persistence:** Postgres for durable state, Redis for presence/matchmaking. The prototype ships
   an in-memory store with JSON snapshots so the whole backend runs and is testable with zero external
   services; the schema mirrors the Postgres tables documented in `server/src/store.js`.

## Explicit limits (do not overstate)

- The shipped server is a **single-process reference implementation**. It has not been load tested.
- No claim of "thousands of concurrent players" is made anywhere until phase 11 load tests pass.
- Auth here is correct but minimal: no email verification, no 2FA, no OAuth providers yet.
