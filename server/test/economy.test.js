import test from 'node:test';
import assert from 'node:assert/strict';

import { config as baseConfig } from '../src/config.js';
import { startTestWorld, post, get, register, uniqueEmail } from './helpers.mjs';

function worldOptions(payments = {}) {
  return {
    config: {
      ...baseConfig,
      adminEmail: 'admin@auralis.test',
      payments: { googlePlay: null, appStore: null, steam: null, stripe: null, ...payments }
    }
  };
}

test('ledger derives balances and enforces non-negative funds', async () => {
  const server = await startTestWorld(worldOptions());
  try {
    const created = await register(server.base);
    const accountId = created.body.accountId;
    const store = server.world.store;

    const credited = store.credit(accountId, { kind: 'currency_grant', delta: 500, ref: 'test:grant:1' });
    assert.equal(credited.duplicate, false);
    assert.equal(store.balanceOf(accountId), 500);

    const overspend = store.credit(accountId, { kind: 'purchase_cosmetic', delta: -900, ref: 'test:spend:1' });
    assert.equal(overspend.error, 'insufficient_funds');
    assert.equal(store.balanceOf(accountId), 500, 'a rejected entry must not change the balance');

    const spent = store.credit(accountId, { kind: 'purchase_cosmetic', delta: -300, ref: 'test:spend:2' });
    assert.equal(spent.entry.balanceAfter, 200);
    assert.equal(store.balanceOf(accountId), 200);
  } finally {
    await server.stop();
  }
});

test('the same receipt can never grant twice', async () => {
  const server = await startTestWorld(worldOptions());
  try {
    const created = await register(server.base);
    const store = server.world.store;
    const accountId = created.body.accountId;

    const first = store.credit(accountId, { kind: 'currency_grant', delta: 100, ref: 'purchase:abc' });
    const replay = store.credit(accountId, { kind: 'currency_grant', delta: 100, ref: 'purchase:abc' });

    assert.equal(first.duplicate, false);
    assert.equal(replay.duplicate, true);
    assert.equal(store.balanceOf(accountId), 100);
    assert.equal(store.ledger.length, 1);
  } finally {
    await server.stop();
  }
});

test('a purchase is refused when the payment provider is not configured', async () => {
  const server = await startTestWorld(worldOptions());
  try {
    const created = await register(server.base);

    const result = await post(server.base, '/v1/iap/verify', {
      platform: 'google_play',
      sku: 'lumina.500',
      receiptId: 'receipt-1',
      requestId: 'req-1'
    }, created.body.token);

    assert.equal(result.status, 501);
    assert.equal(result.body.error, 'provider_not_configured');
    assert.equal(server.world.store.ledger.length, 0, 'nothing may be granted without verification');
  } finally {
    await server.stop();
  }
});

test('a verified purchase grants currency exactly once', async () => {
  const server = await startTestWorld(worldOptions({ googlePlay: 'configured' }));
  try {
    const created = await register(server.base);
    const token = created.body.token;

    const body = {
      platform: 'google_play',
      sku: 'lumina.500',
      receiptId: 'receipt-xyz',
      requestId: 'req-xyz'
    };

    const first = await post(server.base, '/v1/iap/verify', body, token);
    assert.equal(first.status, 200);
    assert.equal(first.body.balance, 500);
    assert.equal(first.body.duplicate, false);

    const replay = await post(server.base, '/v1/iap/verify', body, token);
    assert.equal(replay.status, 200);
    assert.equal(replay.body.duplicate, true);
    assert.equal(replay.body.balance, 500, 'a replayed request must not double the balance');
  } finally {
    await server.stop();
  }
});

test('an amount mismatch is recorded as unverified and grants nothing', async () => {
  const server = await startTestWorld(worldOptions({ appStore: 'configured' }));
  try {
    const created = await register(server.base);

    const result = await post(server.base, '/v1/iap/verify', {
      platform: 'app_store',
      sku: 'lumina.1200',
      receiptId: 'receipt-cheap',
      requestId: 'req-cheap',
      amountMinor: 1
    }, created.body.token);

    assert.equal(result.status, 400);
    assert.equal(result.body.error, 'amount_mismatch');
    assert.equal(server.world.store.balanceOf(created.body.accountId), 0);

    const revenue = server.world.store.revenueSummary();
    assert.equal(revenue.grossMinor, 0);
    assert.equal(revenue.unverifiedAttempts, 1);
  } finally {
    await server.stop();
  }
});

test('spend caps are enforced server-side', async () => {
  const server = await startTestWorld(worldOptions({ googlePlay: 'configured' }));
  try {
    const created = await register(server.base);
    const account = server.world.store.getAccount(created.body.accountId);
    account.spendCapMinor = 1000;

    let blocked = false;
    for (let i = 0; i < 4; i += 1) {
      const result = await post(server.base, '/v1/iap/verify', {
        platform: 'google_play',
        sku: 'lumina.500',
        receiptId: `receipt-cap-${i}`,
        requestId: `req-cap-${i}`
      }, created.body.token);

      if (result.status === 403) {
        assert.equal(result.body.error, 'spend_cap_exceeded');
        blocked = true;
        break;
      }
    }

    assert.equal(blocked, true, 'purchases must stop at the monthly cap');
  } finally {
    await server.stop();
  }
});

test('revenue counts only verified purchases and only admins can read it', async () => {
  const server = await startTestWorld(worldOptions({ googlePlay: 'configured' }));
  try {
    const player = await register(server.base);
    const admin = await register(server.base, 'admin@auralis.test');

    await post(server.base, '/v1/iap/verify', {
      platform: 'google_play', sku: 'lumina.500', receiptId: 'r1', requestId: 'q1'
    }, player.body.token);

    await post(server.base, '/v1/iap/verify', {
      platform: 'google_play', sku: 'cosmetic.outfit.starfall', receiptId: 'r2', requestId: 'q2'
    }, player.body.token);

    const forbidden = await get(server.base, '/v1/admin/revenue', player.body.token);
    assert.equal(forbidden.status, 403);

    const revenue = await get(server.base, '/v1/admin/revenue', admin.body.token);
    assert.equal(revenue.status, 200);
    assert.equal(revenue.body.transactionCount, 2);
    assert.equal(revenue.body.grossMinor, 399 + 499);
    assert.ok(revenue.body.platformFeesMinor > 0, 'platform fees must be deducted');
    assert.equal(revenue.body.netMinor, revenue.body.grossMinor - revenue.body.platformFeesMinor);
  } finally {
    await server.stop();
  }
});

test('the catalogue never sells combat power', async () => {
  const server = await startTestWorld(worldOptions());
  try {
    const catalog = await get(server.base, '/v1/catalog');
    assert.equal(catalog.status, 200);
    assert.ok(catalog.body.products.length > 0);

    for (const product of catalog.body.products) {
      const json = JSON.stringify(product).toLowerCase();
      assert.equal(json.includes('attack'), false, `${product.sku} must not grant attack`);
      assert.equal(json.includes('damage'), false, `${product.sku} must not grant damage`);
      assert.ok(['currency', 'cosmetic', 'season_pass', 'expansion', 'membership'].includes(product.category));
    }
  } finally {
    await server.stop();
  }
});
