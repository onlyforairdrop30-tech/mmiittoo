# Feature dependency map, complexity and dependencies

## 1. Dependency graph (what must exist before what)

```
P1 FOUNDATION
  project + URP + input + Git
      │
      ├──► P2 CHARACTER ──────────────┐
      │      motor, camera, anim, input abstraction
      │      │                        │
      │      ▼                        │
      │   P3 COMBAT ──────┐           │
      │      damage, health, AI, boss │
      │      │                        │
      │      ▼                        │
      │   P4 RPG ─────────┐           │
      │      XP, stats, inventory, quests
      │      │                        │
      │      ▼                        │
      │   P5 WORLD ───────┐           │
      │      region, NPCs, monsters, dungeon, boss
      │      │                        │
      │      ▼                        │
      │   P6 MULTIPLAYER ─┐           │
      │      auth, authority, party, 1v1 PvP
      │      │                        │
      ├──────┴──► P7 SOCIAL ──┐       │
      │            guilds, friends, chat, trade, market
      │            │                  │
      │            ▼                  │
      │        P8 ADVANCED MMO ──┐    │
      │            10 regions, world bosses, raids, tournaments
      │            │                  │
      │            ▼                  │
      │        P9 MONETIZATION ──┐    │
      │            shop, premium currency, season pass, IAP verify, dashboard
      │            │                  │
      │            ▼                  │
      │        P10 CROSS-PLATFORM ─┐
      │            Android/Windows builds, iOS prep, device controls
      │            │                  │
      │            ▼                  │
      │        P11 TESTING ──┐        │
      │            perf, load, security, sandbox payments
      │            │                  │
      │            ▼                  │
      │        P12 RELEASE
      │            assets, privacy, ToS, ratings, compliance
      │
      └──► (pets/mounts, housing/kingdoms, events attach to P5/P8;
           they need RPG + world + social respectively)
```

Hard blockers worth calling out:

- **P9 cannot start before P6** — you cannot verify a purchase without server identity and a ledger.
- **P8 world bosses need P6 sharding** — a 20-player boss is a concurrency problem, not a content one.
- **P10 iOS build needs a Mac + Apple Developer account** (external, paid, manual) — flagged, not assumed.
- **P11 load tests need a deployable shard** — hence after P8.

## 2. Complexity estimate (engineer-weeks, one senior generalist)

| Phase | Scope | Estimate | Risk |
| --- | --- | --- | --- |
| 1 Foundation | project, URP, CI, scene builder | 1 | low |
| 2 Character | motor, camera, procedural anim, input | 2 | medium (feel tuning) |
| 3 Combat | combos, dodge/parry, AI, boss phases | 4 | **high** (feel + netcode later) |
| 4 RPG | XP, stats, inventory, quests, skill tree | 3 | low |
| 5 World | region, NPC, monsters, dungeon, boss | 5 | medium (content volume) |
| 6 Multiplayer | auth, authority, reconciliation, PvP | 8 | **very high** |
| 7 Social | guilds, friends, chat, trade, auction | 6 | high (economy exploits) |
| 8 Advanced MMO | 9 more regions, raids, tournaments | 16+ | high (content + scale) |
| 9 Monetization | shop, currency, season pass, IAP, dashboard | 5 | **very high** (compliance) |
| 10 Cross-platform | builds, store configs, device controls | 4 | medium |
| 11 Testing | perf, load, security, payment sandbox | 6 | high |
| 12 Release | store assets, legal, ratings | 3 | medium |

**Total ≈ 63 engineer-weeks of focused work** for a credible 1.0 with 10 regions at prototype
content density. Art production (characters, monsters, environment, animation) is a separate
multiplier — this repository deliberately uses code-generated placeholders so engineering is not
blocked on art.

## 3. Dependencies

### Free / already available
- Unity 6 LTS + URP + Input System + Cinemachine + Addressables + AI Navigation + uGUI + Test Framework
- Blender (GPL) for original meshes
- Node 24 (backend), Git
- Fonts: Unity built-in `LegacyRuntime.ttf` for the prototype (replaced by a licensed UI font before release)

### Paid / external accounts required (all flagged before use, never auto-purchased)
| Item | Why | Approx cost |
| --- | --- | --- |
| Apple Developer Program | iOS builds + IAP | $99/yr |
| Google Play Console | Android release + Play Billing | $25 once |
| Mac hardware or cloud Mac | iOS compilation | — |
| Steamworks (if PC via Steam) | PC storefront + payments | $100/app |
| Custom server hosting (Postgres + 2 app nodes) | live multiplayer | ~$40–120/mo |
| Email delivery (verification, receipts) | account security | free tier → ~$20/mo |
| CDN / object storage (patch delivery via Addressables) | content updates | usage-based |

### Explicitly rejected
- Paid random loot boxes / gacha (policy + gambling law)
- Any payment SDK that bypasses Apple/Google billing
- Generative AI per NPC at runtime (cost, latency, moderation risk) — behaviour trees instead

## 4. Asset licensing record

See `docs/ASSET_LICENSING.md`. Every asset shipped must be either code-generated in this repository
or added with a recorded license entry.
