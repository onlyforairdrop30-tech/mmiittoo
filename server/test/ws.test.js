import test from 'node:test';
import assert from 'node:assert/strict';

import { startTestWorld, register, WsClient } from './helpers.mjs';

test('websocket: handshake, auth, movement authority, ping', async () => {
  const server = await startTestWorld();
  const client = new WsClient(server.wsUrl);

  try {
    await client.connect();

    const hello = await client.next(message => message.type === 'hello');
    assert.equal(hello.protocol, 1);

    const created = await register(server.base);
    client.send({ type: 'auth', token: created.body.token });

    const authOk = await client.next(message => message.type === 'auth_ok');
    assert.equal(authOk.character.level, 1);
    assert.ok(Array.isArray(authOk.monsters) && authOk.monsters.length > 0);

    // Legal small step.
    client.send({ type: 'move', x: 0.25, z: -7.9 });
    await new Promise(resolve => setTimeout(resolve, 60));

    // Impossible jump.
    client.send({ type: 'move', x: 90, z: 90 });
    const correction = await client.next(message => message.type === 'correction');
    assert.equal(correction.reason, 'speed');
    assert.ok(correction.violations >= 1);

    client.send({ type: 'ping', t: 4242 });
    const pong = await client.next(message => message.type === 'pong');
    assert.equal(pong.t, 4242);

    const state = await client.next(message => message.type === 'state');
    assert.ok(Array.isArray(state.players));
  } finally {
    client.close();
    await server.stop();
  }
});

test('websocket: invalid tokens are refused', async () => {
  const server = await startTestWorld();
  const client = new WsClient(server.wsUrl);

  try {
    await client.connect();
    await client.next(message => message.type === 'hello');

    client.send({ type: 'auth', token: 'forged.token' });
    const error = await client.next(message => message.type === 'auth_error');
    assert.equal(error.reason, 'invalid_token');
  } finally {
    client.close();
    await server.stop();
  }
});

test('websocket: combat is validated and kills are rewarded by the server', async () => {
  const server = await startTestWorld();
  const client = new WsClient(server.wsUrl);

  try {
    await client.connect();
    await client.next(message => message.type === 'hello');

    const created = await register(server.base);
    client.send({ type: 'auth', token: created.body.token });
    const authOk = await client.next(message => message.type === 'auth_ok');

    const monster = authOk.monsters.find(entry => entry.enemyId === 'monster.blight_stalker');
    assert.ok(monster, 'a blight stalker must be in the world');

    // Far away: the server must refuse.
    client.send({ type: 'attack', targetId: monster.id, abilityId: 'light' });
    const denied = await client.next(message => message.type === 'denied');
    assert.equal(denied.reason, 'out_of_range');

    // Move the character next to the monster and soften it so one hit kills.
    const character = server.world.store.getCharacter(created.body.accountId);
    character.position = { x: monster.position.x + 1, y: 0, z: monster.position.z };
    const target = server.world.monsters.get(monster.id);
    target.health = 3;

    client.send({ type: 'attack', targetId: monster.id, abilityId: 'light' });

    const damage = await client.next(message => message.type === 'damage');
    assert.ok(damage.damage > 0);

    const kill = await client.next(message => message.type === 'kill');
    assert.ok(kill.xp > 0, 'the server must award XP for the kill');
    assert.ok(kill.gold >= 0);

    assert.ok(character.xp > 0, 'XP is written server-side');
    assert.ok(character.kills >= 1);
  } finally {
    client.close();
    await server.stop();
  }
});

test('websocket: clients cannot grant themselves XP or items', async () => {
  const server = await startTestWorld();
  const client = new WsClient(server.wsUrl);

  try {
    await client.connect();
    await client.next(message => message.type === 'hello');

    const created = await register(server.base);
    client.send({ type: 'auth', token: created.body.token });
    await client.next(message => message.type === 'auth_ok');

    const character = server.world.store.getCharacter(created.body.accountId);
    const xpBefore = character.xp;
    const goldBefore = character.gold;

    client.send({ type: 'grant_xp', amount: 999999 });
    client.send({ type: 'grant_item', itemId: 'weapon.verdant_blade', count: 99 });

    const error = await client.next(message => message.type === 'error');
    assert.equal(error.reason, 'unknown_message');

    await new Promise(resolve => setTimeout(resolve, 80));

    assert.equal(character.xp, xpBefore, 'XP must not change from a client message');
    assert.equal(character.gold, goldBefore);
    assert.equal(character.inventory.length, 0);
  } finally {
    client.close();
    await server.stop();
  }
});
