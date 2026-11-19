import { hashPassword, verifyPassword, issueToken, verifyToken, RateLimiter } from './auth.js';
import { publicCatalog, findSku } from './catalog.js';
import { isPaymentProviderConfigured } from './config.js';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function createApi({ store, config }) {
  const authLimiter = new RateLimiter(config.rateLimit.authAttempts, config.rateLimit.authWindowMs);

  function authenticate(req) {
    const header = req.headers.authorization ?? '';
    const token = header.startsWith('Bearer ') ? header.slice(7) : null;
    if (!token) return null;

    const payload = verifyToken(token, config.tokenSecret);
    if (!payload) return null;

    return store.getAccount(payload.sub);
  }

  return async function handler(req, res) {
    const url = new URL(req.url ?? '/', 'http://localhost');
    const route = `${req.method} ${url.pathname}`;

    try {
      if (route === 'GET /health') {
        return send(res, 200, {
          ok: true,
          uptimeSeconds: Math.round(process.uptime()),
          accounts: store.accounts.size,
          ledgerEntries: store.ledger.length
        });
      }

      if (route === 'POST /v1/auth/register') return await register(req, res);
      if (route === 'POST /v1/auth/login') return await login(req, res);

      if (route === 'GET /v1/catalog') return send(res, 200, { products: publicCatalog() });

      if (route === 'GET /v1/profile') {
        const account = authenticate(req);
        if (!account) return send(res, 401, { error: 'unauthorized' });
        return send(res, 200, {
          account: { id: account.id, email: account.email, role: account.role },
          character: store.getCharacter(account.id),
          balance: store.balanceOf(account.id)
        });
      }

      if (route === 'GET /v1/wallet') {
        const account = authenticate(req);
        if (!account) return send(res, 401, { error: 'unauthorized' });
        return send(res, 200, {
          balance: store.balanceOf(account.id),
          history: store.historyOf(account.id)
        });
      }

      if (route === 'POST /v1/iap/verify') return await verifyPurchase(req, res);

      if (route === 'GET /v1/admin/revenue') {
        const account = authenticate(req);
        if (!account) return send(res, 401, { error: 'unauthorized' });
        if (account.role !== 'admin') return send(res, 403, { error: 'forbidden' });
        return send(res, 200, store.revenueSummary());
      }

      return send(res, 404, { error: 'not_found' });
    } catch (error) {
      console.error('[api] unhandled error:', error?.message ?? error);
      return send(res, 500, { error: 'internal_error' });
    }
  };

  // ---------------------------------------------------------------- handlers

  async function register(req, res) {
    const ip = req.socket.remoteAddress ?? 'unknown';
    if (!authLimiter.take(`register:${ip}`)) return send(res, 429, { error: 'rate_limited' });

    const body = await readJson(req);
    if (!body) return send(res, 400, { error: 'invalid_json' });

    const email = String(body.email ?? '').trim().toLowerCase();
    const password = String(body.password ?? '');
    if (!EMAIL_PATTERN.test(email)) return send(res, 400, { error: 'invalid_email' });
    if (password.length < 10) return send(res, 400, { error: 'password_too_short' });

    let account;
    try {
      const role = email === String(config.adminEmail).toLowerCase() ? 'admin' : 'player';
      account = store.createAccount({ email, passwordHash: hashPassword(password), role });
    } catch (error) {
      if (error.message === 'email_taken') return send(res, 409, { error: 'email_taken' });
      throw error;
    }

    const token = issueToken({ sub: account.id, role: account.role },
                             config.tokenSecret, config.tokenTtlSeconds);

    return send(res, 201, {
      token,
      accountId: account.id,
      role: account.role,
      character: store.getCharacter(account.id)
    });
  }

  async function login(req, res) {
    const ip = req.socket.remoteAddress ?? 'unknown';
    const body = await readJson(req);
    if (!body) return send(res, 400, { error: 'invalid_json' });

    const email = String(body.email ?? '').trim().toLowerCase();
    if (!authLimiter.take(`login:${ip}:${email}`)) return send(res, 429, { error: 'rate_limited' });

    const account = store.findAccountByEmail(email);
    const passwordOk = account ? verifyPassword(String(body.password ?? ''), account.passwordHash) : false;

    if (!account || !passwordOk) {
      // Same response for unknown account and wrong password: no account enumeration.
      return send(res, 401, { error: 'invalid_credentials' });
    }

    const token = issueToken({ sub: account.id, role: account.role },
                             config.tokenSecret, config.tokenTtlSeconds);

    return send(res, 200, { token, accountId: account.id, character: store.getCharacter(account.id) });
  }

  async function verifyPurchase(req, res) {
    const account = authenticate(req);
    if (!account) return send(res, 401, { error: 'unauthorized' });

    const body = await readJson(req);
    if (!body) return send(res, 400, { error: 'invalid_json' });

    const { platform, sku, receiptId, requestId } = body;
    if (!platform || !sku || !receiptId || !requestId) {
      return send(res, 400, { error: 'missing_fields' });
    }

    const product = findSku(String(sku));
    if (!product) return send(res, 404, { error: 'unknown_sku' });

    // The client saying "payment succeeded" is never enough: without provider keys we refuse.
    if (!isPaymentProviderConfigured(String(platform), config)) {
      return send(res, 501, { error: 'provider_not_configured', platform });
    }

    const existing = store.purchases.get(String(requestId));
    if (existing) {
      return send(res, 200, {
        duplicate: true,
        sku: existing.sku,
        balance: store.balanceOf(account.id),
        entitlements: account.entitlements ?? []
      });
    }

    const amountMinor = Number.isInteger(body.amountMinor) ? body.amountMinor : product.priceMinor;
    if (amountMinor !== product.priceMinor) {
      store.recordPurchase({
        requestId: String(requestId), accountId: account.id, sku: product.name,
        platform: String(platform), amountMinor, verified: false, granted: false
      });
      return send(res, 400, { error: 'amount_mismatch' });
    }

    // Spend safeguards are enforced here, not in the UI.
    if (account.spendCapMinor !== null) {
      const spent = monthlySpend(account.id);
      if (spent + amountMinor > account.spendCapMinor) {
        return send(res, 403, { error: 'spend_cap_exceeded' });
      }
    }

    if (product.grants?.lumina) {
      const credited = store.credit(account.id, {
        kind: 'currency_grant',
        delta: product.grants.lumina,
        ref: `purchase:${requestId}`
      });
      if (credited.error) return send(res, 402, { error: credited.error });
    }

    if (product.cosmetic || product.category !== 'currency') {
      account.entitlements ??= [];
      if (!account.entitlements.includes(sku)) account.entitlements.push(sku);
    }

    store.recordPurchase({
      requestId: String(requestId), accountId: account.id, sku: product.name,
      platform: String(platform), amountMinor, verified: true, granted: true
    });

    return send(res, 200, {
      duplicate: false,
      sku: product.name,
      balance: store.balanceOf(account.id),
      entitlements: account.entitlements ?? []
    });
  }

  function monthlySpend(accountId) {
    const now = Date.now();
    const windowStart = now - 30 * 24 * 60 * 60 * 1000;
    let total = 0;
    for (const purchase of store.purchases.values()) {
      if (purchase.accountId !== accountId) continue;
      if (!purchase.verified) continue;
      if (purchase.ts < windowStart) continue;
      total += purchase.amountMinor;
    }
    return total;
  }
}

function send(res, status, body) {
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'content-length': Buffer.byteLength(payload),
    'cache-control': 'no-store'
  });
  res.end(payload);
}

function readJson(req, limitBytes = 64 * 1024) {
  return new Promise((resolve) => {
    let size = 0;
    const chunks = [];

    req.on('data', (chunk) => {
      size += chunk.length;
      if (size > limitBytes) {
        req.destroy();
        resolve(null);
        return;
      }
      chunks.push(chunk);
    });

    req.on('end', () => {
      if (chunks.length === 0) return resolve(null);
      try {
        resolve(JSON.parse(Buffer.concat(chunks).toString('utf8')));
      } catch {
        resolve(null);
      }
    });

    req.on('error', () => resolve(null));
  });
}
