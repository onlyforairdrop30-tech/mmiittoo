// Authentication primitives: scrypt password hashing, compact HMAC-signed session tokens and a
// fixed-window rate limiter. No external dependencies, so this is auditable in one file.
import { randomBytes, scryptSync, timingSafeEqual, createHmac } from 'node:crypto';

const SCRYPT_KEY_LENGTH = 32;

export function hashPassword(password) {
  if (typeof password !== 'string' || password.length < 10) {
    throw new Error('password_too_short');
  }
  if (password.length > 256) throw new Error('password_too_long');

  const salt = randomBytes(16);
  const derived = scryptSync(password, salt, SCRYPT_KEY_LENGTH);
  return `scrypt$${salt.toString('hex')}$${derived.toString('hex')}`;
}

export function verifyPassword(password, stored) {
  if (typeof password !== 'string' || typeof stored !== 'string') return false;

  const parts = stored.split('$');
  if (parts.length !== 3 || parts[0] !== 'scrypt') return false;

  let salt;
  let expected;
  try {
    salt = Buffer.from(parts[1], 'hex');
    expected = Buffer.from(parts[2], 'hex');
  } catch {
    return false;
  }
  if (salt.length === 0 || expected.length !== SCRYPT_KEY_LENGTH) return false;

  const derived = scryptSync(password, salt, SCRYPT_KEY_LENGTH);
  return timingSafeEqual(derived, expected);
}

const encode = (value) => Buffer.from(value).toString('base64url');

/**
 * Issues a compact token: base64url(payload).hmacSha256. Deliberately not a full JWT — we only
 * need integrity and expiry, and a smaller format is easier to audit.
 */
export function issueToken(payload, secret, ttlSeconds, now = Date.now()) {
  const issuedAt = Math.floor(now / 1000);
  const body = { ...payload, iat: issuedAt, exp: issuedAt + ttlSeconds };
  const encoded = encode(JSON.stringify(body));
  const signature = createHmac('sha256', secret).update(encoded).digest('base64url');
  return `${encoded}.${signature}`;
}

export function verifyToken(token, secret, now = Date.now()) {
  if (typeof token !== 'string') return null;

  const separator = token.lastIndexOf('.');
  if (separator <= 0) return null;

  const encoded = token.slice(0, separator);
  const signature = token.slice(separator + 1);
  const expected = createHmac('sha256', secret).update(encoded).digest('base64url');

  const actualBuffer = Buffer.from(signature);
  const expectedBuffer = Buffer.from(expected);
  if (actualBuffer.length !== expectedBuffer.length) return null;
  if (!timingSafeEqual(actualBuffer, expectedBuffer)) return null;

  let payload;
  try {
    payload = JSON.parse(Buffer.from(encoded, 'base64url').toString('utf8'));
  } catch {
    return null;
  }

  if (typeof payload !== 'object' || payload === null) return null;
  if (typeof payload.exp !== 'number') return null;
  if (payload.exp * 1000 <= now) return null;
  if (typeof payload.sub !== 'string') return null;

  return payload;
}

/** Fixed-window limiter. `take` returns true while the caller is under budget. */
export class RateLimiter {
  constructor(limit, windowMs) {
    this.limit = limit;
    this.windowMs = windowMs;
    this.buckets = new Map();
  }

  take(key, now = Date.now()) {
    const bucket = this.buckets.get(key);
    if (!bucket || now - bucket.start >= this.windowMs) {
      this.buckets.set(key, { start: now, count: 1 });
      return true;
    }
    if (bucket.count >= this.limit) return false;
    bucket.count += 1;
    return true;
  }

  reset(key) {
    this.buckets.delete(key);
  }
}
