# Monetization design (fair, auditable, platform-compliant)

Revenue exists so the world keeps running. Nothing sold here makes a player stronger in combat,
and no purchase is required to reach any content.

## 1. Catalogue

| Product | Contents | Power effect |
| --- | --- | --- |
| **Cosmetic outfit** | full-body skin set (character) | none |
| **Cosmetic accessory** | hair, eyes, glow effects, profile frame, emote, mount/pet *appearance* | none |
| **Housing decor** | furniture, garden props | none (space is earned) |
| **Season pass** (600–900 ✦) | free track (all players) + premium track (cosmetics, emotes, frames, currency) | none |
| **Expansion pack** | new story chapter, one region, missions, cosmetic collection | none — the region is also reachable by non-owners as a guest in party play |
| **Supporter membership** | cosmetic aura, extra loadout slot, monthly ✦ stipend, nameplate frame | none |
| **Premium currency (✦ Lumina)** | 100 / 500 / 1200 / 2400 tiers | n/a |

**Never sold:** gear with stats, XP boosts that affect ranked play, loot boxes, keys, betting,
timed discounts, "buy now" pressure, anything directed at children.

## 2. Currency and economy integrity

- Two currencies: **gold** (earned) and **Lumina ✦** (purchased). Neither is client-authoritative.
- Every mutation appends to an **append-only ledger** (`server/src/store.js`): `{id, accountId, kind,
  delta, balanceAfter, ref, ts}`. Balances are *derived* from the ledger and reconciled on login.
- Grants from purchases require a **verified platform receipt**; the client's "purchase succeeded"
  message is never trusted (`POST /v1/iap/verify` in `server/src/httpApi.js`).
- Idempotency: every purchase carries a client-generated `requestId`; replaying it returns the same
  ledger entry instead of granting twice (tested in `server/test/economy.test.js`).
- Refunds append a compensating entry; the derived balance handles clawback without editing history.

## 3. Payment integrations (planned, phase 9)

| Platform | API | Verification |
| --- | --- | --- |
| Android | **Google Play Billing Library 7** | `purchases.products.get` on our server with a service-account key |
| iOS | **StoreKit 2** | App Store Server API (`/inApps/v1/transactions/{id}`) + App Store Server Notifications V2 for refunds |
| Windows (Steam) | **Steamworks microtransactions** | `ISteamMicroTxn` InitTxn/QueryTxn server-side |
| Windows (direct) | **Stripe Checkout** (web purchase of ✦ only) | webhook signature + `checkout.session.completed` |
| Web | Stripe, only where the platform's rules permit it (never for iOS digital goods) | as above |

All of these need accounts and keys the project owner must create. **Nothing is enabled by default**;
`server/src/config.js` reads them from env and the verify endpoint returns `501 not_configured`
until keys are present.

## 4. Safety and transparency

- Price shown before confirmation; total including tax; refund policy link on the store page.
- Parental controls: 4-digit purchase PIN, per-month spend cap, store disabled by default on
  child accounts. Enforced server-side (`account.spendCapMinor`), not in the UI.
- Playtime reminders every 60/120 min; no streak-loss, no energy gates, no login FOMO loops.
- Purchase history page lists every transaction with its receipt id.

## 5. Revenue dashboard (admin only)

`GET /v1/admin/revenue` (requires `role=admin` + admin token) returns:

```
grossMinor, netMinor, platformFeesMinor, refundsMinor,
byProduct[{sku, count, grossMinor}], byCurrency, byPlatform,
currencySales, cosmeticSales, seasonPassSales, expansionSales,
unverifiedAttempts (fraud signal)
```

Only **verified** receipts count toward revenue. Customer PII and payment tokens are never stored —
we keep `receiptId`, `sku`, `amountMinor`, `platform`, `accountId`, `ts`.

## 6. What is implemented today

- Ledger, idempotent grants, balance derivation, spend caps, unverified-attempt counter, admin revenue
  aggregation, rate limiting — all in `server/` with passing tests.
- Cosmetic store *catalog* and purchase flow *rules* are server-side; the in-game shop UI is a later
  phase (phase 9) so we do not ship a store that cannot take a real, compliant payment.
