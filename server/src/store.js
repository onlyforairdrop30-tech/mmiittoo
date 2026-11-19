// Authoritative state store. In-memory for the reference implementation, with the same shape as
// the production Postgres schema documented in docs/BACKEND_DECISION.md:
//   accounts(id, email, password_hash, role, spend_cap_minor, created_at)
//   characters(id, account_id, name, level, xp, x, y, z, health, gold)
//   ledger(id, account_id, kind, delta, balance_after, ref, ts)   -- append only
//   purchases(request_id PK, account_id, sku, platform, amount_minor, verified, ts)

export class Store {
  constructor() {
    this.accounts = new Map();
    this.accountsByEmail = new Map();
    this.characters = new Map();
    this.ledger = [];
    this.ledgerByRef = new Map();
    this.purchases = new Map();
    this.sequence = 0;
  }

  nextId(prefix) {
    this.sequence += 1;
    return `${prefix}_${this.sequence.toString(36)}`;
  }

  // ---------------------------------------------------------------- accounts

  createAccount({ email, passwordHash, role = 'player' }) {
    const normalized = String(email).trim().toLowerCase();
    if (this.accountsByEmail.has(normalized)) throw new Error('email_taken');

    const id = this.nextId('acc');
    const account = {
      id,
      email: normalized,
      passwordHash,
      role,
      spendCapMinor: null,
      createdAt: Date.now()
    };

    this.accounts.set(id, account);
    this.accountsByEmail.set(normalized, id);
    this.characters.set(id, createDefaultCharacter(id));
    return account;
  }

  findAccountByEmail(email) {
    const id = this.accountsByEmail.get(String(email).trim().toLowerCase());
    return id ? this.accounts.get(id) : null;
  }

  getAccount(id) {
    return this.accounts.get(id) ?? null;
  }

  getCharacter(accountId) {
    return this.characters.get(accountId) ?? null;
  }

  // ---------------------------------------------------------------- ledger

  balanceOf(accountId) {
    let total = 0;
    for (const entry of this.ledger) {
      if (entry.accountId === accountId) total += entry.delta;
    }
    return total;
  }

  /**
   * Appends a balance change. `ref` makes the operation idempotent: replaying the same receipt or
   * request id returns the original entry instead of granting twice.
   */
  credit(accountId, { kind, delta, ref = null }) {
    if (!Number.isInteger(delta) || delta === 0) return { error: 'invalid_delta' };
    if (!this.accounts.has(accountId)) return { error: 'unknown_account' };

    if (ref !== null) {
      const existing = this.ledgerByRef.get(ref);
      if (existing) return { entry: existing, duplicate: true };
    }

    const balanceAfter = this.balanceOf(accountId) + delta;
    if (balanceAfter < 0) return { error: 'insufficient_funds' };

    const entry = {
      id: this.nextId('txn'),
      accountId,
      kind,
      delta,
      balanceAfter,
      ref,
      ts: Date.now()
    };

    this.ledger.push(entry);
    if (ref !== null) this.ledgerByRef.set(ref, entry);
    return { entry, duplicate: false };
  }

  historyOf(accountId, limit = 50) {
    return this.ledger.filter((entry) => entry.accountId === accountId).slice(-limit).reverse();
  }

  // ---------------------------------------------------------------- purchases

  recordPurchase({ requestId, accountId, sku, platform, amountMinor, verified, granted }) {
    const existing = this.purchases.get(requestId);
    if (existing) return { purchase: existing, duplicate: true };

    const purchase = {
      requestId,
      accountId,
      sku,
      platform,
      amountMinor,
      verified,
      granted,
      ts: Date.now()
    };
    this.purchases.set(requestId, purchase);
    return { purchase, duplicate: false };
  }

  /** Revenue only counts verified transactions; unverified attempts are reported separately. */
  revenueSummary() {
    const summary = {
      grossMinor: 0,
      netMinor: 0,
      platformFeesMinor: 0,
      refundsMinor: 0,
      transactionCount: 0,
      unverifiedAttempts: 0,
      byProduct: {},
      byPlatform: {},
      byCategory: { currency: 0, cosmetic: 0, season_pass: 0, expansion: 0, membership: 0 }
    };

    for (const purchase of this.purchases.values()) {
      if (!purchase.verified) {
        summary.unverifiedAttempts += 1;
        continue;
      }

      summary.transactionCount += 1;
      summary.grossMinor += purchase.amountMinor;

      const fee = Math.round(purchase.amountMinor * feeRateFor(purchase.platform));
      summary.platformFeesMinor += fee;
      summary.netMinor += purchase.amountMinor - fee;

      const product = summary.byProduct[purchase.sku] ?? { count: 0, grossMinor: 0 };
      product.count += 1;
      product.grossMinor += purchase.amountMinor;
      summary.byProduct[purchase.sku] = product;

      const platform = summary.byPlatform[purchase.platform] ?? { count: 0, grossMinor: 0 };
      platform.count += 1;
      platform.grossMinor += purchase.amountMinor;
      summary.byPlatform[purchase.platform] = platform;
    }

    for (const entry of this.ledger) {
      if (entry.kind === 'refund') summary.refundsMinor += Math.abs(entry.delta);
      if (entry.kind === 'purchase_cosmetic') summary.byCategory.cosmetic += Math.abs(entry.delta);
      if (entry.kind === 'purchase_season_pass') summary.byCategory.season_pass += Math.abs(entry.delta);
      if (entry.kind === 'purchase_expansion') summary.byCategory.expansion += Math.abs(entry.delta);
      if (entry.kind === 'currency_grant') summary.byCategory.currency += entry.delta;
    }

    return summary;
  }
}

function feeRateFor(platform) {
  switch (platform) {
    case 'google_play': return 0.15;
    case 'app_store': return 0.15;
    case 'steam': return 0.30;
    case 'stripe': return 0.029;
    default: return 0.15;
  }
}

export function createDefaultCharacter(accountId) {
  return {
    id: accountId,
    accountId,
    name: 'Wanderer',
    level: 1,
    xp: 0,
    position: { x: 0, y: 0.2, z: -8 },
    health: 120,
    maxHealth: 120,
    attack: 20,
    defense: 6,
    energy: 100,
    gold: 0,
    inventory: [],
    kills: 0,
    lastAttackAt: 0,
    lastMoveAt: 0,
    sessionId: null
  };
}
