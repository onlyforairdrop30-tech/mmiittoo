# Architecture

## 1. Runtime layers (Unity)

```
Assets/Scripts
  Core/         GameManager, service access, global events, game rules constants
  Player/       PlayerMotor (CharacterController), InputRouter, PlayerInputReader, ThirdPersonCamera
  Animation/    ProceduralAnimator (code-driven limb animation; no external rigs)
  Combat/       IDamageable, DamageInfo, CombatFormulas, Health, PlayerHealth, PlayerCombat, HitQuery
  Enemies/      EnemyDefinition (SO), EnemyBrain (FSM + NavMeshAgent), BossBrain (phase FSM), LootTable
  Progression/  XpCurve, PlayerProgression, StatBlock
  Inventory/    ItemDefinition (SO), ItemDatabase (SO), InventoryModel, EquipmentLoadout
  Quests/       QuestDefinition (SO), QuestObjective, QuestLog
  World/        DayNightCycle, WeatherCycle, Pickup, FastTravelPoint, Interactable
  UI/           HudBuilder (constructs uGUI), HudController, PanelInventory, TouchControls
  Networking/   NetClient (System.Net.WebSockets + JSON), NetMessages, NetSync
```

Rules that keep it maintainable:

- **No singleton soup.** `GameManager` is the only scene singleton; everything else is wired by the
  scene builder into serialized references, so the graph is visible in the Inspector.
- **Pure logic is engine-free where possible** (`XpCurve`, `CombatFormulas`, `InventoryModel`,
  `StatBlock`). These are unit-tested in `Assets/Tests/EditMode` and are the same rules the backend
  re-implements for authority.
- **Data is ScriptableObjects**, not hardcoded switch statements: items, enemies and quests are
  assets created by `ContentSeeder`, so designers add content without touching code.
- **Input is abstracted** (`InputRouter`), so the same player code serves keyboard/mouse, touch
  joystick and future gamepad. Mobile never forks gameplay code.

## 2. Why this shape

An MMO dies from coupling, not from missing features. The scene builder generating the prototype
means the shipped project has **one authoritative scene definition in code** — reviewable in a PR,
reproducible on CI, and impossible to drift from the scripts.

## 3. Combat pipeline (client prediction, server authority)

```
Input → PlayerCombat (local state machine, prediction)
      → HitQuery (overlap sphere, layer mask) → IDamageable.ApplyDamage(DamageInfo)
      → NetClient.Send(AttackIntent { abilityId, seq, targetId, position })
Server: validate cooldown, range, facing, speed budget → compute damage from
        content table + defending stats → ApplyDamage → broadcast authoritative HP
Client: reconcile HP, roll back on mismatch
```

Local prediction keeps the prototype responsive single-player; the *only* trusted source is the
server once networking is on (`docs/ROADMAP.md` phase 6).

## 4. Scaling model (backend)

```
           ┌─────────────┐
 clients ──┤ Gateway/LB  ├── regional (EU/NA/APAC/SA)
           └──────┬──────┘
                  │ authenticated session (HMAC token, 15 min, refresh)
        ┌─────────┴──────────┐
        │ World shard (zone) │  authoritative sim, 20 Hz
        └─────┬────────┬─────┘
              │        │
        ┌─────┴───┐ ┌──┴──────────┐
        │ Postgres│ │ Redis       │  profiles/inventory/ledger | presence, sessions, matchmaking
        └─────────┘ └─────────────┘
```

- Zone shards are stateless-ish: state is flushed to Postgres; a crash costs one tick.
- Matchmaking and chat are separate services so a world restart never drops a queue.
- **We do not claim concurrency numbers.** `docs/ROADMAP.md` phase 11 defines the k6/Artillery load
  test that must pass before any number is published.

## 5. Graphics tiers

| Tier | Target | Shadows | Render scale | Post | Actors |
| --- | --- | --- | --- | --- | --- |
| Low | low-end Android | off / 1 cascade | 0.75 | none | 20 |
| Medium | mid Android | 2 cascade, 512 | 0.9 | bloom | 40 |
| High | modern phone / laptop | 4 cascade, 1024 | 1.0 | bloom+ACES | 60 |
| Ultra | gaming PC | 4 cascade, 2048 | 1.0 + MSAA | full | 60 |

Implemented by `QualityTier` in `Core/GameRules.cs`, applied on first boot from a hardware query and
overridable in Settings.
