# PROJECT INFINITY

An original 3D anime-inspired open-world action MMORPG for **Windows, Android and iOS**, built with
Unity 6 LTS. Everything here is original IP: the setting (*Auralis*), races, classes, monsters,
regions, character and audio are generated from primitives and code — no third-party or anime assets.

## What is in this repository

| Path | Contents |
| --- | --- |
| `ProjectInfinity/` | The Unity 6 LTS project (URP, Input System, Cinemachine, Addressables, AI Navigation) |
| `ProjectInfinity/Assets/Scripts/` | All gameplay systems (player, combat, AI, progression, inventory, quests, UI, world, net) |
| `ProjectInfinity/Assets/Editor/` | Tooling that **builds the playable prototype scene, content assets, URP setup and build targets** |
| `ProjectInfinity/Assets/Tests/` | EditMode tests for pure game-rule logic |
| `server/` | Authoritative multiplayer backend (Node 24, JWT-style auth, server-side simulation, ledger) |
| `docs/` | Architecture, backend decision record, dependency map, monetization design, roadmap, licensing |

## Current status (honest)

**Verified on this machine:**
- Backend: `npm test` in `server/` — authentication, token forgery, rate limiting, movement speed
  validation, combat range/cooldown validation, XP authority, inventory integrity and the currency
  ledger all pass automated tests.

**Not verified here:** Unity is not installed on this machine, so the C# has **not** been compiled and
the prototype has **not** been run in Play Mode. The Unity side is delivered as source plus a
scene builder; open it in Unity 6 LTS and run `Project Infinity ▸ Build Prototype` (or the
`Tools/Prototype` menu) — see *Run the prototype* below. Any compile error will be in a single file
and the builder logs which step failed.

## Run the prototype (Unity)

1. Open `ProjectInfinity/` with **Unity 6 LTS (6000.0.x)** using the Android/iOS/Windows modules.
2. Wait for the package import (URP, Input System, Cinemachine, Addressables, AI Navigation).
3. Menu **Project Infinity → 1. Configure Project** (URP asset, quality tiers, input, build targets).
4. Menu **Project Infinity → 2. Build Prototype Scene** — generates `Assets/Scenes/Prototype_Auralis.unity`:
   an original humanoid from primitives, the *Verdant Hollow* area, three enemy species, the
   *Rootwarden* boss, baked NavMesh, HUD, touch controls.
5. Press **Play**.

### Controls

| Action | PC | Mobile |
| --- | --- | --- |
| Move | `WASD` | left virtual stick |
| Camera | mouse | drag anywhere |
| Sprint | hold `Shift` | Sprint button (hold) |
| Light combo (3 hits) | `LMB` | Strike button |
| Heavy attack | `E` | Heavy button |
| Block / parry (tap) | hold `RMB` | Block button (hold) |
| Dodge (i-frames) | `Space` | Dodge button |
| Abilities | `Q` / `R` / `T` | on-screen ability buttons |
| Ultimate | `F` | Awakening button |
| Interact | `G` | — (pickups are touch-collected) |
| Inventory | `Tab` | Close button on the panel |
| Release cursor | `Esc` | n/a |

## Quick Start

### Windows (1-Click)
Run `start.bat` in the repository root to start the backend server and launch Unity.

### Docker (Multiplayer Backend)
```bash
docker compose up -d
```
The authoritative server will be running on `ws://127.0.0.1:8787` with health checks at `http://127.0.0.1:8787/health`.

To stop:
```bash
docker compose down
```

## Run the backend directly

```bash
npm start          # ws://127.0.0.1:8787
# or
cd server && npm start
```

## Continuous integration

`.github/workflows/unity-ci.yml` uses [GameCI](https://game.ci) to compile the project and run the
EditMode suite on **every push and pull request**, then build a Linux x64 player (which also proves
the editor tooling can generate the prototype scene from a clean checkout).

Add these repository secrets before the first run (Settings → Secrets and variables → Actions):

| Secret | Value |
| --- | --- |
| `UNITY_LICENSE` | contents of your `.ulf` licence file (Personal licences) |
| `UNITY_EMAIL` | your Unity account email |
| `UNITY_PASSWORD` | your Unity account password (alphanumeric; special characters break activation) |
| `UNITY_SERIAL` | Plus/Pro serial — used instead of `UNITY_LICENSE` |

Without the secrets the jobs fail at the activation step; nothing else in the repository is affected.
CI runs the same code locally with `Project Infinity → 1. Configure Project` and
`2. Build Prototype Scene`, so a red CI log is reproducible on a desktop.

## Design pillars

1. **Skill over stats** — timing, spacing and parry windows beat level gaps; progression is capped
   at level 60 with diminishing returns (no unbounded inflation).
2. **Server authority** — damage, XP, loot, currency and purchases are computed server-side; the
   client sends *intent* only.
3. **Fair monetization** — cosmetics, season pass and expansions only. No power, no loot boxes,
   no gambling.
4. **One world, three platforms** — same accounts, same characters, latency-aware sharding,
   separate ranked pools for touch and mouse input.
