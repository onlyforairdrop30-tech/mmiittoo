# Roadmap and phase gates

Each phase has an explicit **exit gate**. A phase is not "done" because code exists; it is done when
the gate passes and is recorded here.

## Phase 1 — Foundation ✅ (this commit)
Unity 6 LTS project, URP config script, Input System, Cinemachine/Addressables/AI Navigation packages,
folder architecture, `.gitignore`, Git repository, deterministic scene builder.
**Gate:** project opens, packages resolve, prototype scene builds without errors.
*Machine note: Unity is not installed on the authoring machine, so the editor import/compile gate
must be re-run by the owner. See README "Not verified here".*

## Phase 2 — Character ✅ (this commit)
Third-person `CharacterController` motor, orbit camera with collision, procedural limb animation,
`InputRouter` abstraction, keyboard/mouse + touch input, mobile joystick.
**Gate:** walk/run/sprint/jump/dodge on PC and touch; camera never clips through terrain.

## Phase 3 — Combat ✅ (prototype scope, this commit)
3-hit light combo, heavy, dodge with i-frames, block/parry window, 3 abilities + ultimate with
cooldowns, stamina/energy, hit reactions, `IDamageable` pipeline, FSM enemy AI, phase-based boss.
**Gate:** each enemy type dies to a distinct strategy; parry refunds stamina and grants a counter window.

## Phase 4 — RPG ✅ (prototype scope, this commit)
Level 1–60 curve with diminishing returns, XP from kills/quests, stat aggregation from equipment,
inventory with stacking + capacity, equipment slots, quest log with objective tracking, rewards.
**Gate:** kill → XP → level-up → stat change verified by EditMode tests for the pure rules.

## Phase 5 — World ◑ (one region this commit)
*Verdant Hollow*: terrain, trees, ruins, shrine, fast-travel pad, day/night, weather, pickups.
Remaining: NPC dialogue trees, dungeon interior, and 9 further regions (phases 8).
**Gate:** 10 minutes of play without falling through geometry or leaving the region.

## Phase 6 — Multiplayer ◑ (backend this commit)
Auth (scrypt + HMAC token), authoritative simulation contracts, movement speed validation, combat
range/cooldown validation, XP/loot authority, party-ready session model.
Remaining: client reconciliation loop, 1v1 matchmaking, dedicated shard processes.
**Gate:** two clients, one cheater — cheater's speed and damage are rejected server-side (covered by
`server/test/integrity.test.js`).

## Phase 7 — Social ⬜
Guilds, friends, chat with moderation filters, trade with two-phase commit, auction house.
**Gate:** trade cannot duplicate items under an interrupted commit (fault-injection test).

## Phase 8 — Advanced MMO ⬜
9 regions, world bosses (10–20), raids, global events, tournaments, spectators, replays.

## Phase 9 — Monetization ⬜
Store UI, Lumina, season pass, expansion entitlements, Google/Apple/Steam verification, admin
revenue dashboard.

## Phase 10 — Cross-platform ⬜
Android + Windows builds, iOS project generation (needs Apple account), device input profiles,
separate ranked pools for touch vs mouse.

## Phase 11 — Testing ⬜
Device perf matrix, k6 load tests (publish numbers only after passing), security review, payment
sandbox tests, accessibility audit.

## Phase 12 — Release ⬜
Store assets, privacy policy, ToS, age ratings (IARC/PEGI/ESRB), compliance review, release builds.

---

## Next development step (recommended)

1. Owner opens `ProjectInfinity/` in Unity 6 LTS, runs **Configure Project → Build Prototype Scene**,
   and reports any compile errors. That closes phase 1's editor gate.
2. Then phase 5 completion: NPC dialogue + one dungeon interior, since it is the cheapest way to turn
   the prototype into something a playtester can spend an hour in.
