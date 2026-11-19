import test from 'node:test';
import assert from 'node:assert/strict';

import { hashPassword, verifyPassword, issueToken, verifyToken, RateLimiter } from '../src/auth.js';
import { startTestWorld, post, get, register, uniqueEmail } from './helpers.mjs';

const SECRET = 'test-secret-do-not-use-in-production';

test('password hashing round-trips and rejects wrong passwords', () => {
  const stored = hashPassword('correct horse battery');
  assert.equal(verifyPassword('correct horse battery', stored), true);
  assert.equal(verifyPassword('correct horse batteru', stored), false);
  assert.equal(verifyPassword('', stored), false);
  assert.equal(verifyPassword('correct horse battery', 'garbage'), false);
});

test('password hashing salts uniquely', () => {
  const a = hashPassword('correct horse battery');
  const b = hashPassword('correct horse battery');
  assert.notEqual(a, b);
});

test('short passwords are rejected', () => {
  assert.throws(() => hashPassword('short'), /password_too_short/);
});

test('tokens verify and carry the subject', () => {
  const token = issueToken({ sub: 'acc_1', role: 'player' }, SECRET, 60);
  const payload = verifyToken(token, SECRET);
  assert.equal(payload.sub, 'acc_1');
  assert.equal(payload.role, 'player');
});

test('tampered tokens are rejected', () => {
  const token = issueToken({ sub: 'acc_1', role: 'player' }, SECRET, 60);
  const [body] = token.split('.');

  const forgedBody = Buffer.from(JSON.stringify({ sub: 'acc_2', exp: 9999999999 })).toString('base64url');
  assert.equal(verifyToken(`${forgedBody}.${token.split('.')[1]}`, SECRET), null);
  assert.equal(verifyToken(`${body}.deadbeef`, SECRET), null);
  assert.equal(verifyToken('not-a-token', SECRET), null);
  assert.equal(verifyToken(token, 'different-secret'), null);
});

test('expired tokens are rejected', () => {
  const now = Date.now();
  const token = issueToken({ sub: 'acc_1' }, SECRET, 60, now - 120_000);
  assert.equal(verifyToken(token, SECRET, now), null);
});

test('rate limiter blocks past its budget and resets after the window', () => {
  const limiter = new RateLimiter(3, 1000);
  const t0 = 1_000_000;

  assert.equal(limiter.take('a', t0), true);
  assert.equal(limiter.take('a', t0 + 10), true);
  assert.equal(limiter.take('a', t0 + 20), true);
  assert.equal(limiter.take('a', t0 + 30), false);
  assert.equal(limiter.take('b', t0 + 30), true, 'other keys keep their own budget');
  assert.equal(limiter.take('a', t0 + 1200), true, 'window rollover resets the bucket');
});

test('register -> login -> profile', async () => {
  const server = await startTestWorld();
  try {
    const email = uniqueEmail();
    const password = 'correct horse battery';

    const created = await register(server.base, email, password);
    assert.equal(created.status, 201);
    assert.ok(created.body.token);
    assert.equal(created.body.character.level, 1);

    const profile = await get(server.base, '/v1/profile', created.body.token);
    assert.equal(profile.status, 200);
    assert.equal(profile.body.account.email, email);
    assert.equal(profile.body.balance, 0);

    const login = await post(server.base, '/v1/auth/login', { email, password });
    assert.equal(login.status, 200);
    assert.ok(login.body.token);
  } finally {
    await server.stop();
  }
});

test('duplicate email is rejected', async () => {
  const server = await startTestWorld();
  try {
    const email = uniqueEmail();
    assert.equal((await register(server.base, email)).status, 201);
    assert.equal((await register(server.base, email)).status, 409);
  } finally {
    await server.stop();
  }
});

test('wrong password and unknown account give the same answer', async () => {
  const server = await startTestWorld();
  try {
    const email = uniqueEmail();
    await register(server.base, email, 'correct horse battery');

    const wrong = await post(server.base, '/v1/auth/login', { email, password: 'wrong password!!' });
    const unknown = await post(server.base, '/v1/auth/login',
      { email: 'nobody@auralis.test', password: 'correct horse battery' });

    assert.equal(wrong.status, 401);
    assert.equal(unknown.status, 401);
    assert.deepEqual(wrong.body, unknown.body);
  } finally {
    await server.stop();
  }
});

test('profile rejects missing or forged tokens', async () => {
  const server = await startTestWorld();
  try {
    assert.equal((await get(server.base, '/v1/profile')).status, 401);
    assert.equal((await get(server.base, '/v1/profile', 'forged.token')).status, 401);
  } finally {
    await server.stop();
  }
});

test('short password is rejected at the API boundary', async () => {
  const server = await startTestWorld();
  try {
    const result = await register(server.base, uniqueEmail(), 'short');
    assert.equal(result.status, 400);
    assert.equal(result.body.error, 'password_too_short');
  } finally {
    await server.stop();
  }
});

test('login attempts are rate limited', async () => {
  const server = await startTestWorld();
  try {
    const email = uniqueEmail();
    await register(server.base, email, 'correct horse battery');

    let sawRateLimit = false;
    for (let attempt = 0; attempt < 15; attempt += 1) {
      const result = await post(server.base, '/v1/auth/login', { email, password: 'nope nope nope' });
      if (result.status === 429) {
        sawRateLimit = true;
        break;
      }
    }

    assert.equal(sawRateLimit, true, 'repeated failures must be throttled');
  } finally {
    await server.stop();
  }
});
