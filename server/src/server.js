import { createServer } from 'node:http';
import { pathToFileURL } from 'node:url';

import { config as defaultConfig } from './config.js';
import { Store } from './store.js';
import { createApi } from './httpApi.js';
import { attachWebSocketServer } from './ws.js';
import { verifyToken, RateLimiter } from './auth.js';
import { applyAttack } from './sim/combat.js';
import { grantKillXp } from './sim/xp.js';
import { validateMove } from './sim/movement.js';
import { ENEMIES, mulberry32, rollGold, rollLoot } from './sim/loot.js';

const MONSTER_SPAWNS = [
  { enemyId: 'monster.blight_stalker', position: { x: 8, y: 0, z: 2 } },
  { enemyId: 'monster.blight_stalker', position: { x: -9, y: 0, z: 5 } },
  { enemyId: 'monster.blight_stalker', position: { x: 14, y: 0, z: -6 } },
  { enemyId: 'monster.ember_wisp', position: { x: -13, y: 0, z: -2 } },
  { enemyId: 'monster.ember_wisp', position: { x: 5, y: 0, z: 12 } },
  { enemyId: 'monster.stone_sentry', position: { x: -4, y: 0, z: 16 } },
  { enemyId: 'monster.rootwarden', position: { x: 0, y: 0, z: 40 } }
];

export function createWorld(options = {}) {
  const cfg = options.config ?? defaultConfig;
  const store = options.store ?? new Store();
  const random = mulberry32(options.seed ?? 20261119);

  const monsters = new Map();
  const sessions = new Set();

  let nextMonsterId = 1;
  for (const spawn of MONSTER_SPAWNS) {
    const definition = ENEMIES[spawn.enemyId];
    if (!definition) continue;
    const id = `mob_${nextMonsterId++}`;
    monsters.set(id, {
      id,
      enemyId: spawn.enemyId,
      level: definition.level,
      baseXp: definition.baseXp,
      defense: definition.defense,
      maxHealth: definition.maxHealth,
      health: definition.maxHealth,
      position: { ...spawn.position },
      alive: true
    });
  }

  const api = createApi({ store, config: cfg });
  const httpServer = createServer(api);
  attachWebSocketServer(httpServer, (socket) => handleConnection(socket));

  const tickMs = Math.max(20, Math.round(1000 / Math.max(1, cfg.tickHz)));
  const tick = setInterval(broadcastSnapshot, tickMs);
  if (typeof tick.unref === 'function') tick.unref();

  // ---------------------------------------------------------------- websocket

  function handleConnection(socket) {
    const connection = {
      socket,
      account: null,
      character: null,
      lastMoveAt: Date.now(),
      violations: 0,
      limiter: new RateLimiter(cfg.rateLimit.messagesPerSecond, 1000)
    };

    socket.on('message', (raw) => {
      if (!connection.limiter.take('messages')) {
        socket.sendJson({ type: 'error', reason: 'rate_limited' });
        return;
      }

      let message;
      try {
        message = JSON.parse(raw);
      } catch {
        socket.sendJson({ type: 'error', reason: 'invalid_json' });
        return;
      }

      route(connection, message);
    });

    socket.on('close', () => {
      sessions.delete(connection);
      if (connection.character) connection.character.sessionId = null;
    });

    socket.sendJson({ type: 'hello', protocol: 1, tickHz: cfg.tickHz });
  }

  function route(connection, message) {
    switch (message.type) {
      case 'auth': return authenticate(connection, message);
      case 'move': return move(connection, message);
      case 'attack': return attack(connection, message);
      case 'ping': return connection.socket.sendJson({ type: 'pong', t: message.t ?? Date.now() });
      default: return connection.socket.sendJson({ type: 'error', reason: 'unknown_message' });
    }
  }

  function authenticate(connection, message) {
    const payload = verifyToken(message.token, cfg.tokenSecret);
    if (!payload) {
      connection.socket.sendJson({ type: 'auth_error', reason: 'invalid_token' });
      return;
    }

    const account = store.getAccount(payload.sub);
    if (!account) {
      connection.socket.sendJson({ type: 'auth_error', reason: 'unknown_account' });
      return;
    }

    connection.account = account;
    connection.character = store.getCharacter(account.id);
    connection.lastMoveAt = Date.now();
    sessions.add(connection);

    connection.socket.sendJson({
      type: 'auth_ok',
      accountId: account.id,
      character: connection.character,
      monsters: [...monsters.values()].map(m => ({ id: m.id, enemyId: m.enemyId, health: m.health, position: m.position }))
    });
  }

  /**
   * Movement authority: the client reports its position, the server checks the implied speed.
   * A rejected move is answered with a correction instead of being applied.
   */
  function move(connection, message) {
    const character = connection.character;
    if (!character) return connection.socket.sendJson({ type: 'error', reason: 'not_authenticated' });

    const now = Date.now();
    const dt = Math.min(2, Math.max(0.001, (now - connection.lastMoveAt) / 1000));
    connection.lastMoveAt = now;

    const verdict = validateMove({
      from: character.position,
      to: { x: Number(message.x), y: Number(message.y) || 0, z: Number(message.z) },
      dtSeconds: dt,
      maxSpeed: cfg.maxRunSpeed,
      tolerance: cfg.speedTolerance
    });

    if (!verdict.ok) {
      connection.violations += 1;
      return connection.socket.sendJson({
        type: 'correction',
        reason: verdict.reason,
        position: character.position,
        violations: connection.violations
      });
    }

    character.position = verdict.position;
    // Energy regenerates on the server; the client only displays it.
    character.energy = Math.min(100, (character.energy ?? 0) + 6.5 * dt);
    return undefined;
  }

  function attack(connection, message) {
    const character = connection.character;
    if (!character) return connection.socket.sendJson({ type: 'error', reason: 'not_authenticated' });

    const target = monsters.get(String(message.targetId));
    if (!target || !target.alive) {
      return connection.socket.sendJson({ type: 'denied', reason: 'invalid_target' });
    }

    if (character.health <= 0) {
      return connection.socket.sendJson({ type: 'denied', reason: 'dead' });
    }

    const result = applyAttack({
      attacker: character,
      target,
      abilityId: String(message.abilityId ?? 'light'),
      now: Date.now()
    });

    if (!result.ok) return connection.socket.sendJson({ type: 'denied', reason: result.reason });

    broadcast({
      type: 'damage',
      attackerId: character.id,
      targetId: target.id,
      damage: result.damage,
      targetHealth: result.targetHealth
    });

    if (result.killed) onMonsterKilled(connection, target);
    return undefined;
  }

  function onMonsterKilled(connection, monster) {
    const definition = ENEMIES[monster.enemyId];
    const character = connection.character;
    monster.alive = false;
    monster.health = 0;

    const xpResult = definition ? grantKillXp(character, definition) : { xp: 0, levelsGained: 0 };
    const gold = definition ? rollGold(definition.goldReward, random) : 0;
    const drops = definition ? rollLoot(definition.loot, random) : [];

    character.gold += gold;
    for (const drop of drops) character.inventory.push(drop);
    character.kills = (character.kills ?? 0) + 1;

    broadcast({
      type: 'kill',
      monsterId: monster.id,
      enemyId: monster.enemyId,
      xp: xpResult.xp,
      levelsGained: xpResult.levelsGained,
      level: character.level,
      gold,
      drops
    });

    const respawn = setTimeout(() => {
      monster.alive = true;
      monster.health = monster.maxHealth;
    }, 20000);
    if (typeof respawn.unref === 'function') respawn.unref();
  }

  // ---------------------------------------------------------------- broadcast

  function broadcastSnapshot() {
    if (sessions.size === 0) return;

    const players = [...sessions]
      .filter(session => session.character)
      .map(session => ({
        id: session.character.id,
        name: session.character.name,
        level: session.character.level,
        health: session.character.health,
        position: session.character.position
      }));

    const monsterState = [...monsters.values()]
      .filter(monster => monster.alive)
      .map(monster => ({ id: monster.id, enemyId: monster.enemyId, health: monster.health, position: monster.position }));

    const frame = JSON.stringify({ type: 'state', players, monsters: monsterState });

    for (const session of sessions) {
      if (!session.socket.closed) session.socket.send(frame);
    }
  }

  function broadcast(message) {
    const payload = JSON.stringify(message);
    for (const session of sessions) {
      if (!session.socket.closed) session.socket.send(payload);
    }
  }

  // ---------------------------------------------------------------- lifecycle

  function listen(port = cfg.port, host = cfg.host) {
    return new Promise((resolve, reject) => {
      httpServer.once('error', reject);
      httpServer.listen(port, host, () => {
        const address = httpServer.address();
        resolve({ port: address.port, host: address.address });
      });
    });
  }

  function close() {
    clearInterval(tick);
    for (const session of sessions) session.socket.close(1001);
    sessions.clear();
    return new Promise(resolve => httpServer.close(() => resolve()));
  }

  return { store, config: cfg, httpServer, monsters, sessions, listen, close };
}

// Only start listening when executed directly (`npm start`), not when imported by tests.
const executedDirectly = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;

if (executedDirectly) {
  const world = createWorld();
  world.listen().then(({ port, host }) => {
    console.log(`[Project Infinity] authoritative server listening on ws://${host}:${port}`);
    console.log(`[Project Infinity] http api on http://${host}:${port}/health`);
    console.log('[Project Infinity] payment providers:',
      Object.entries(defaultConfig.payments).filter(([, v]) => v).map(([k]) => k).join(', ') || 'none configured');
  }).catch((error) => {
    console.error('[Project Infinity] failed to start:', error.message);
    process.exitCode = 1;
  });
}
