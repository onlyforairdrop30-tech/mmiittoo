# Project Infinity — authoritative server

Reference implementation of the multiplayer authority layer. **Zero npm dependencies**: the
WebSocket server, token format, rate limiter and ledger are all in this directory so the whole
backend can be read and audited in one sitting.

```bash
node --test      # 46 tests: auth, integrity, economy, end-to-end websocket
npm start        # ws://127.0.0.1:8787  +  http://127.0.0.1:8787/health
```

## What the server decides (never the client)

| Rule | Module |
| --- | --- |
| Passwords (scrypt), session tokens (HMAC-SHA256), rate limits | `src/auth.js` |
| Movement speed, world bounds, position correction | `src/sim/movement.js` |
| Damage, ability cooldown, range, energy | `src/sim/combat.js` |
| XP, level-ups, derived stats | `src/sim/xp.js` |
| Loot, gold, enemy content table | `src/sim/loot.js` |
| Currency balance, idempotent grants, refunds | `src/store.js` (append-only ledger) |
| Purchase verification, spend caps, revenue | `src/httpApi.js`, `src/catalog.js` |

## HTTP API

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/health` | — | liveness |
| POST | `/v1/auth/register` | — | create account, returns token |
| POST | `/v1/auth/login` | — | returns token |
| GET | `/v1/profile` | bearer | account + character + balance |
| GET | `/v1/wallet` | bearer | balance + ledger history |
| GET | `/v1/catalog` | — | store catalogue (cosmetic only) |
| POST | `/v1/iap/verify` | bearer | verify a platform receipt and grant |
| GET | `/v1/admin/revenue` | admin bearer | revenue dashboard data |

`/v1/iap/verify` answers **501 `provider_not_configured`** until real Google Play / App Store /
Steam / Stripe keys are supplied via environment variables. It never trusts a client-side
"purchase succeeded" message.

## WebSocket protocol

```jsonc
// client -> server
{ "type": "auth", "token": "<session token>" }
{ "type": "move", "x": 0.2, "y": 0, "z": -8, "seq": 1 }
{ "type": "attack", "targetId": "mob_1", "abilityId": "light", "seq": 2 }
{ "type": "ping", "t": 1731000000000 }

// server -> client
{ "type": "hello", "protocol": 1, "tickHz": 20 }
{ "type": "auth_ok", "accountId": "acc_1", "character": { ... } }
{ "type": "correction", "reason": "speed", "position": { ... }, "violations": 1 }
{ "type": "damage", "targetId": "mob_1", "damage": 19.6, "targetHealth": 90.4 }
{ "type": "kill", "monsterId": "mob_1", "xp": 55, "gold": 12, "drops": [...] }
{ "type": "state", "players": [...], "monsters": [...] }   // 20 Hz snapshot
{ "type": "error", "reason": "unknown_message" }
```

Any message that would mutate progression (`grant_xp`, `grant_item`, …) is answered with
`unknown_message` and changes nothing — covered by `test/ws.test.js`.

## Not production yet

Single process, in-memory state, no persistence, no load test. Before any concurrency claim:
Postgres/Redis backing, multi-shard routing, and the phase-11 load test in `docs/ROADMAP.md`.
